using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 氷柱（Network版）
/// - せり上がり：サーバーが位置を動かす（NetworkTransformで同期）
/// - 当たり判定：氷柱側 OnCollisionEnter / OnTriggerEnter
///     - サーバー：直接HP減
///     - クライアント：ServerRpcで「当たった」を報告 → サーバーがHP減
/// - ヒット演出：ClientRpc で VisualRoot を少し揺らす（見た目だけ）
/// - 破壊：サーバーで判定して Broken を NetworkVariable で配布
/// </summary>
public class IcePillar : NetworkBehaviour
{
    [Header("Rise (Server drives position)")]
    [SerializeField, Min(0f)] private float buryDepth = 2.0f;
    [SerializeField, Min(0f)] private float riseHeight = 3.0f;
    [SerializeField, Min(0.01f)] private float riseDuration = 1.2f;

    [Header("Hit Shake (Visual only)")]
    [SerializeField] private Transform visualRoot; // 見た目だけ揺らす（Rootは揺らさない）
    [SerializeField, Min(0f)] private float hitShakeAmplitude = 0.10f;
    [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.10f;
    [SerializeField, Min(0f)] private float hitShakeFrequency = 22f;

    [Header("HP (Server Authoritative)")]
    [SerializeField, Min(1f)] private float maxHP = 60f;

    [Header("Damage (Pillar decides)")]
    [SerializeField, Min(0f)] private float damageMin = 0.5f;
    [SerializeField, Min(0f)] private float damageMax = 1.25f;

    [Header("Hit Control")]
    [SerializeField, Min(0f)] private float hitCooldown = 0.08f; // 多段ヒット抑制（サーバーで確定）
    [SerializeField] private string punchTag = ""; // Tagで判定したいなら設定（空ならPunchコンポーネント判定）

    [Header("Break Visual/Collision")]
    [SerializeField] private GameObject iceVisualObject; // 氷メッシュまとめ（折れたら消す）
    [SerializeField] private Collider[] collidersToDisableOnBreak;

    [Header("Wagashi Attach")]
    [SerializeField] private BoxCollider attachVolume;
    [SerializeField] private NetworkPrefabDatabase prefabDatabase;
    [SerializeField] private Vector2Int wagashiCountRange = new(5, 6);
    [SerializeField, Min(0f)] private float wagashiReleaseImpulse = 2.0f;

    // 壊れたかどうかは全員で共有
    private readonly NetworkVariable<bool> _broken = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _hp;

    // attackerClientId -> lastHitTime（サーバー側で多段抑制）
    private readonly Dictionary<ulong, float> _lastHitTime = new();

    // 生成した和菓子（サーバーが保持）
    private readonly List<FrozenWagashi> _spawnedWagashi = new();

    private Vector3 _basePos;

    public override void OnNetworkSpawn()
    {
        if (visualRoot == null) visualRoot = transform;
        if (iceVisualObject == null && visualRoot != null) iceVisualObject = visualRoot.gameObject;

        if (attachVolume == null) attachVolume = GetComponentInChildren<BoxCollider>();

        _broken.OnValueChanged += OnBrokenChanged;

        if (IsServer)
        {
            _hp = maxHP;
            StartCoroutine(RiseRoutineServer());
        }
    }

    private void OnDestroy()
    {
        _broken.OnValueChanged -= OnBrokenChanged;
    }

    private void OnBrokenChanged(bool prev, bool next)
    {
        if (!next) return;

        if (iceVisualObject != null) iceVisualObject.SetActive(false);

        if (collidersToDisableOnBreak != null)
        {
            foreach (var c in collidersToDisableOnBreak)
            {
                if (c != null) c.enabled = false;
            }
        }
    }

    private IEnumerator RiseRoutineServer()
    {
        if (!IsServer) yield break;

        _basePos = transform.position;

        // 最初は地面下
        transform.position = _basePos + Vector3.down * buryDepth;

        // せり上がり開始時に和菓子を生成（サーバーでSpawn）
        SpawnFrozenWagashiServer();

        float t = 0f;
        float totalUp = buryDepth + riseHeight;

        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / riseDuration);

            float up = Mathf.SmoothStep(0f, totalUp, n);
            transform.position = (_basePos + Vector3.down * buryDepth) + Vector3.up * up;

            yield return null;
        }

        transform.position = _basePos;
    }

    private void SpawnFrozenWagashiServer()
    {
        if (!IsServer) return;

        if (attachVolume == null)
        {
            Debug.LogWarning("[IcePillar] attachVolume が未設定です");
            return;
        }

        if (prefabDatabase == null || prefabDatabase._entries == null || prefabDatabase._entries.Count == 0)
        {
            Debug.LogWarning("[IcePillar] prefabDatabase が空です");
            return;
        }

        _spawnedWagashi.Clear();

        int count = Random.Range(wagashiCountRange.x, wagashiCountRange.y + 1);

        for (int i = 0; i < count; i++)
        {
            var entry = prefabDatabase._entries[Random.Range(0, prefabDatabase._entries.Count)];
            var prefab = entry._prefab;
            if (prefab == null) continue;

            Vector3 localInBox = GetRandomPointInLocalBox(attachVolume);
            Quaternion localRot = Random.rotation;

            Vector3 worldPos = attachVolume.transform.TransformPoint(localInBox);
            Quaternion worldRot = attachVolume.transform.rotation * localRot;

            var go = Instantiate(prefab, worldPos, worldRot);

            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogWarning("[IcePillar] 生成した和菓子に NetworkObject がありません");
                Destroy(go);
                continue;
            }

            no.Spawn(true);

            var fw = go.GetComponent<FrozenWagashi>();
            if (fw == null)
            {
                Debug.LogWarning("[IcePillar] 生成した和菓子に FrozenWagashi がありません");
                continue;
            }

            // NetworkVariableの仕組みに任せる（OnFrozenChangedを直呼びしない）
            fw.SetFrozenServer(true);

            // 氷柱ローカルで追従情報をセット（Scale継承されない方式）
            Vector3 localPosToPillar = transform.InverseTransformPoint(worldPos);
            Quaternion localRotToPillar = Quaternion.Inverse(transform.rotation) * worldRot;

            fw.AttachFollowServer(transform, localPosToPillar, localRotToPillar);

            _spawnedWagashi.Add(fw);
        }
    }

    private Vector3 GetRandomPointInLocalBox(BoxCollider box)
    {
        Vector3 half = box.size * 0.5f;

        float x = Random.Range(-half.x, half.x);
        float y = Random.Range(-half.y, half.y);
        float z = Random.Range(-half.z, half.z);

        return box.center + new Vector3(x, y, z);
    }

    // ------------------------------
    // ダメージ受付（Collision / Trigger 両対応）
    // ------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (_broken.Value) return;

        // 当たったコライダー側から Punch を探す（gameObject だと親に当たったりするので collider優先）
        var col = collision.collider;
        if (!IsPunchHit(col)) return;

        // 衝撃方向（ざっくり）
        Vector3 hitDir = collision.relativeVelocity.sqrMagnitude > 0.001f
            ? collision.relativeVelocity.normalized
            : (col.transform.position - transform.position).normalized;

        Vector3 hitPoint = collision.GetContact(0).point;

        HandlePunchHit(col, hitPoint, hitDir);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_broken.Value) return;


        Debug.Log("当たってはいる");

        if (!IsPunchHit(other)) return;

        // Triggerは接触点が取りづらいので、近い点で代用
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitDir = (other.transform.position - transform.position).normalized;
        if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

        HandlePunchHit(other, hitPoint, hitDir);
    }

    /// <summary>
    /// 「パンチが当たった」を受けて、
    /// サーバーなら即確定、クライアントならServerRpcで報告する
    /// </summary>
    private void HandlePunchHit(Collider punchCollider, Vector3 hitPoint, Vector3 hitDir)
    {
        // まず「このパンチはローカルプレイヤーのものか？」をチェックして
        // ローカルのパンチだけがServerRpcを送る（他人のパンチで送らない）
        if (!IsServer)
        {
            if (!IsLocalPlayersPunch(punchCollider)) return;

            // クライアント→サーバーへ報告（ダメージはサーバーが決める）
            ReportPunchHitServerRpc(hitPoint, hitDir);
            return;
        }

        // サーバーなら直接確定
        ulong attackerId = GetOwnerClientIdFromPunch(punchCollider);
        ApplyDamageServer(attackerId, hitPoint, hitDir);
    }

    /// <summary>
    /// パンチ判定：TagかPunchコンポーネントで判定
    /// </summary>
    private bool IsPunchHit(Collider col)
    {
        
        // Punchスクリプトが拳側に付いている前提（子でもOK）
        return col.GetComponent<Punch>() != null;
    }

    /// <summary>
    /// そのColliderが「ローカルプレイヤー（この端末のプレイヤー）の拳」かどうか
    /// </summary>
    private bool IsLocalPlayersPunch(Collider col)
    {
        if (NetworkManager.Singleton == null) return false;

        // 拳がプレイヤー階層下にある前提：親のNetworkObjectからOwnerを取る
        var ownerNO = col.GetComponent<NetworkObject>();
        if (ownerNO == null) return false;

        return ownerNO.OwnerClientId == NetworkManager.Singleton.LocalClientId;
    }

    /// <summary>
    /// サーバー側で攻撃者IDを推定（取れない場合もあるのでフォールバック）
    /// </summary>
    private ulong GetOwnerClientIdFromPunch(Collider col)
    {
        var ownerNO = col.GetComponentInParent<NetworkObject>();
        if (ownerNO != null) return ownerNO.OwnerClientId;

        // 推定できない場合は「0」として扱う（クールダウン用途なのでOK）
        return 0;
    }

    // ------------------------------
    // サーバー確定処理
    // ------------------------------

    [ServerRpc(RequireOwnership = false)]
    private void ReportPunchHitServerRpc(Vector3 hitPoint, Vector3 hitDir, ServerRpcParams rpcParams = default)
    {
        if (_broken.Value) return;

        // 送信者 = 攻撃者として扱う（偽装しにくい）
        ulong attackerId = rpcParams.Receive.SenderClientId;
        ApplyDamageServer(attackerId, hitPoint, hitDir);
    }

    private void ApplyDamageServer(ulong attackerClientId, Vector3 hitPoint, Vector3 hitDir)
    {
        if (!IsServer) return;
        if (_broken.Value) return;

        // 多段ヒット抑制（サーバーで確定）
        float now = Time.time;
        if (_lastHitTime.TryGetValue(attackerClientId, out var last))
        {
            if (now - last < hitCooldown) return;
        }
        _lastHitTime[attackerClientId] = now;

        // ランダムダメージはサーバーで決定
        float damage = Random.Range(damageMin, damageMax);

        // ダメージに応じて揺れ強さも少し変える
        float amp = Mathf.Lerp(0.06f, 0.14f, Mathf.InverseLerp(damageMin, damageMax, damage));
        PlayHitShakeClientRpc(amp, hitShakeDuration, hitShakeFrequency);

        _hp -= damage;

        if (_hp <= 0f)
        {
            BreakServer(hitDir);
        }
    }

    [ClientRpc]
    private void PlayHitShakeClientRpc(float amplitude, float duration, float frequency)
    {
        if (visualRoot == null) return;
        StartCoroutine(HitShakeRoutineLocal(visualRoot, amplitude, duration, frequency));
    }

    private IEnumerator HitShakeRoutineLocal(Transform target, float amplitude, float duration, float frequency)
    {
        Vector3 baseLocalPos = target.localPosition;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float damp = 1f - n;

            float nx = (Mathf.PerlinNoise(Time.time * frequency, 0.1f) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(0.2f, Time.time * frequency) - 0.5f) * 2f;

            Vector3 offset = new Vector3(nx, 0f, nz) * amplitude * damp;
            target.localPosition = baseLocalPos + offset;

            yield return null;
        }

        target.localPosition = baseLocalPos;
    }

    private void BreakServer(Vector3 hitDir)
    {
        if (!IsServer) return;
        if (_broken.Value) return;

        _broken.Value = true;

        ReleaseWagashiServer(hitDir);

        //氷柱が壊れたエフェクトを出す
        Vector3 pos = transform.position;

        //↑
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x, pos.y + 2f, pos.z), Quaternion.identity, new Vector3(2f, 2f, 2f));
        //真ん中
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x, pos.y + 1f, pos.z), Quaternion.identity, new Vector3(2f, 2f, 2f));
        //↓
        NetworkEffectSpawner.Instance.PlayEffect(11, pos, Quaternion.identity,new Vector3(2f,2f,2f));
        


        StartCoroutine(DespawnAfterSecondsServer(2.0f));
    }

    private void ReleaseWagashiServer(Vector3 hitDir)
    {
        for (int i = 0; i < _spawnedWagashi.Count; i++)
        {
            var w = _spawnedWagashi[i];
            if (w == null) continue;

            // 追従方式なら親子解除は不要だけど、親子付けしてる場合に備えて安全に外す
            if (w.NetworkObject != null)
                w.NetworkObject.TryRemoveParent();

            w.SetFrozenServer(false);

            var rb = w.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(hitDir * wagashiReleaseImpulse, ForceMode.Impulse);
            }
        }

        _spawnedWagashi.Clear();
    }

    private IEnumerator DespawnAfterSecondsServer(float sec)
    {
        yield return new WaitForSeconds(sec);

        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
