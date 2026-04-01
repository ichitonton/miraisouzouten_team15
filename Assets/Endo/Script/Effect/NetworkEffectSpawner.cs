using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkEffectSpawner : NetworkBehaviour
{
    public static NetworkEffectSpawner Instance { get; private set; }

    [Header("エフェクトのデータベース")]
    [SerializeField] private EffectDatabase _effectDatabase;

    [Header("Pooling")]
    [SerializeField] private Transform _poolRoot;                 // 返却先（空なら自分）
    [SerializeField] private int _prewarmPerEffect = 0;

    // effectId -> inactive instances
    private readonly Dictionary<int, Queue<GameObject>> _pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkEffectSpawner] 重複インスタンスがあったので削除しました");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_poolRoot == null) _poolRoot = transform;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (_prewarmPerEffect > 0 && _effectDatabase != null)
        {
            foreach (var e in _effectDatabase.Effects)
            {
                if (e?.prefab == null) continue;
                Prewarm(e.effectId, e.prefab, _prewarmPerEffect);
            }
        }
    }

    // =============================
    // Public API
    // =============================

    /// <summary>
    /// ただのワールド再生（親なし）
    /// </summary>
    public void PlayEffect(int effectId, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            SpawnEffectLocal(effectId, position, rotation, parent: null, Vector3.zero);
            return;
        }

        if (IsServer)
            PlayEffectClientRpc(effectId, position, rotation,new Vector3(1f,1f,1f));
        else
            RequestPlayEffectServerRpc(effectId, position, rotation,new Vector3(1f, 1f, 1f));
    }

    public void PlayEffect(int effectId, Vector3 position, Quaternion rotation,Vector3 scale)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            SpawnEffectLocal(effectId, position, rotation, parent: null,scale);
            return;
        }

        if (IsServer)
            PlayEffectClientRpc(effectId, position, rotation,scale);
        else
            RequestPlayEffectServerRpc(effectId, position, rotation,scale);
    }

    public void PlayEffect(string effectKey, Vector3 position, Quaternion rotation)
    {
        int effectId = _effectDatabase.GetEffectId(effectKey);
        if (effectId < 0) return;
        PlayEffect(effectId, position, rotation);
    }
    public void PlayEffect(string effectKey, Vector3 position, Quaternion rotation , Vector3 scale)
    {
        int effectId = _effectDatabase.GetEffectId(effectKey);
        if (effectId < 0) return;
        PlayEffect(effectId, position, rotation, scale);
    }

    /// <summary>
    /// 親(NetworkObject)に追従させたい場合（全クライアントで同じ親になる）
    /// localOffset は親のローカル座標系
    /// </summary>
    public void PlayEffectAttached(int effectId, NetworkObject parent, Vector3 localOffset, Quaternion localRotation)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            // オフラインなら親のTransformをそのまま使う
            var p = parent != null ? parent.transform : null;
            Vector3 pos = (p != null) ? p.TransformPoint(localOffset) : localOffset;
            Quaternion rot = (p != null) ? (p.rotation * localRotation) : localRotation;
            SpawnEffectLocal(effectId, pos, rot, p, Vector3.zero);
            return;
        }

        NetworkObjectReference parentRef = default;
        if (parent != null)
            parentRef = new NetworkObjectReference(parent);

        if (IsServer)
            PlayEffectAttachedClientRpc(effectId, parentRef, localOffset, localRotation);
        else
            RequestPlayEffectAttachedServerRpc(effectId, parentRef, localOffset, localRotation);
    }

    // =============================
    // Local Spawn
    // =============================

    private void SpawnEffectLocal(int effectId, Vector3 position, Quaternion rotation, Transform parent,Vector3 scale)
    {
        if (_effectDatabase == null)
        {
            Debug.LogError("[NetworkEffectSpawner] EffectDatabase が設定されていません");
            return;
        }

        var prefab = _effectDatabase.GetEffectPrefab(effectId);
        if (prefab == null)
        {
            Debug.Log("このエフェクトidに登録されてないで");
            return;
        }
        var go = Rent(effectId, prefab);

        // ★毎回親を確定（プール再利用でも正しくなる）
        if (scale != Vector3.zero)
        {
            go.transform.localScale = scale;
            foreach(var g in go.GetComponentsInChildren<Transform>(true))
            {
                g.localScale = scale;
            }
        }
        go.transform.SetParent(parent != null ? parent : _poolRoot, false);

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
    }

    // =============================
    // Pool
    // =============================

    private GameObject Rent(int effectId, GameObject prefab)
    {
        if (_pool.TryGetValue(effectId, out var q))
        {
            while (q.Count > 0)
            {
                var go = q.Dequeue();
                if (go != null) return go;
            }
        }

        // 新規生成は一旦 poolRoot 配下で作る（親はSpawn時に付け替える）
        var inst = Instantiate(prefab, _poolRoot);
        inst.name = $"{prefab.name} (Pooled:{effectId})";

        var pe = inst.GetComponent<PooledEffect>();
        if (pe == null) pe = inst.AddComponent<PooledEffect>();
        pe.Setup(this, effectId);

        inst.SetActive(false);
        return inst;
    }

    public void ReturnToPool(GameObject go, int effectId)
    {
        if (go == null) return;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        go.transform.SetParent(_poolRoot, false);
        go.SetActive(false);

        if (!_pool.TryGetValue(effectId, out var q))
        {
            q = new Queue<GameObject>();
            _pool.Add(effectId, q);
        }
        q.Enqueue(go);
    }

    private void Prewarm(int effectId, GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = Rent(effectId, prefab);
            ReturnToPool(go, effectId);
        }
    }

    // =============================
    // RPC
    // =============================

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayEffectServerRpc(int effectId, Vector3 position, Quaternion rotation,Vector3 scale)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        PlayEffectClientRpc(effectId, position, rotation,scale);
    }

    [ClientRpc]
    private void PlayEffectClientRpc(int effectId, Vector3 position, Quaternion rotation,Vector3 scale)
    {
        SpawnEffectLocal(effectId, position, rotation, parent: null,scale);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayEffectAttachedServerRpc(int effectId, NetworkObjectReference parentRef, Vector3 localOffset, Quaternion localRot)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        PlayEffectAttachedClientRpc(effectId, parentRef, localOffset, localRot);
    }

    [ClientRpc]
    private void PlayEffectAttachedClientRpc(int effectId, NetworkObjectReference parentRef, Vector3 localOffset, Quaternion localRot)
    {
        Transform parent = null;
        if (parentRef.TryGet(out var netObj) && netObj != null)
            parent = netObj.transform;

        // 親のローカル空間で位置/回転を決める
        //Vector3 pos = parent != null ? parent.TransformPoint(localOffset) : localOffset;
        //Quaternion rot = parent != null ? (parent.rotation * localRot) : localRot;

        SpawnEffectLocal(effectId, localOffset, localRot, parent, Vector3.zero);
    }
}
