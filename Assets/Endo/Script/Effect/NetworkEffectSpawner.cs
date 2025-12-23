using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkEffectSpawner : NetworkBehaviour
{
    public static NetworkEffectSpawner Instance { get; private set; }

    [Header("エフェクトのデータベース")]
    [SerializeField] private EffectDatabase _effectDatabase;

    [Header("Pooling")]
    [SerializeField] private Transform _poolRoot;   // 空でOK（空なら自分のtransform配下にする）
    [SerializeField] private int _prewarmPerEffect = 0;

    // effectId -> inactive instances
    private readonly Dictionary<int, Queue<GameObject>> _pool = new();
    // instance -> effectId
    private readonly Dictionary<GameObject, int> _instanceToId = new();

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
        // 任意：プリウォーム
        if (_prewarmPerEffect > 0 && _effectDatabase != null)
        {
            foreach (var e in _effectDatabase.Effects)
            {
                if (e?.prefab == null) continue;
                Prewarm(e.effectId, e.prefab, _prewarmPerEffect);
            }
        }
    }

    public void PlayEffect(int effectId, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            SpawnEffectLocal(effectId, position, rotation);
            return;
        }

        if (IsServer)
        {
            PlayEffectClientRpc(effectId, position, rotation);
        }
        else
        {
            RequestPlayEffectServerRpc(effectId, position, rotation);
        }
    }

    public void PlayEffect(string effectKey, Vector3 position, Quaternion rotation)
    {
        int effectId = _effectDatabase.GetEffectId(effectKey);
        if (effectId < 0) return;
        PlayEffect(effectId, position, rotation);
    }

    private void SpawnEffectLocal(int effectId, Vector3 position, Quaternion rotation)
    {
        if (_effectDatabase == null)
        {
            Debug.LogError("[NetworkEffectSpawner] EffectDatabase が設定されていません");
            return;
        }

        var prefab = _effectDatabase.GetEffectPrefab(effectId);
        if (prefab == null) return;

        var go = Rent(effectId, prefab);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true); // ここで PooledEffect の OnEnable が走って自己返却が始まる
    }

    // --- Pool API ---
    private GameObject Rent(int effectId, GameObject prefab)
    {
        if (_pool.TryGetValue(effectId, out var q) && q.Count > 0)
        {
            var go = q.Dequeue();
            // 念のため null 混入時の保険
            if (go != null) return go;
        }

        var inst = Instantiate(prefab, _poolRoot);
        inst.name = $"{prefab.name} (Pooled:{effectId})";
        _instanceToId[inst] = effectId;

        var pe = inst.GetComponent<PooledEffect>();
        if (pe == null) pe = inst.AddComponent<PooledEffect>();
        pe.Setup(this, effectId);

        inst.SetActive(false);
        return inst;
    }

    public void ReturnToPool(GameObject go, int effectId)
    {
        if (go == null) return;

        // Stopしてから返す（残り粒子が次回に残るのを避けたいなら）
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
            // Rentは非アクティブで返すので、そのままプールに積む
            ReturnToPool(go, effectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayEffectServerRpc(int effectId, Vector3 position, Quaternion rotation,
        ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        PlayEffectClientRpc(effectId, position, rotation);
    }

    [ClientRpc]
    private void PlayEffectClientRpc(int effectId, Vector3 position, Quaternion rotation,
        ClientRpcParams clientRpcParams = default)
    {
        SpawnEffectLocal(effectId, position, rotation);
    }
}
