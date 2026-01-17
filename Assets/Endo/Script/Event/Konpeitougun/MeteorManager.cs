using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MeteorManager : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float spinSpeed = 360f;

    // ==============================
    // ★追加：炎エフェクト（Attached）
    // ==============================
    [Header("Trail Fire Effect (Attached)")]
    [SerializeField] private int[] fireEffectId;
    [SerializeField] private Vector3 fireLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 fireAxisOffsetEuler = Vector3.zero;
    [SerializeField] private bool fireFaceAgainstVelocity = true;
    [SerializeField] private float fireRotationSlerp = 25f;

    private List<Transform> _fireEffectTransform = new List<Transform>();
    private bool _fireSpawnRequested = false;

    [SerializeField] private GameObject _fireSpawnPrefab = null;

    [Header("Konpeito Prefabs (NetworkObject付き)")]
    [SerializeField] private NetworkObject[] konpeitoPrefabs;

    [Header("Impact Knockback")]
    [SerializeField] private float knockbackRadius = 5f;
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float knockbackUpward = 2f;
    [SerializeField] private LayerMask knockbackMask = ~0;
    [SerializeField] private bool affectKonpeitoToo = false;

    [Header("Konpeito Burst (8～11)")]
    [SerializeField] private int minKonpeito = 8;
    [SerializeField] private int maxKonpeito = 11;
    [SerializeField] private float burstSpeed = 4f;
    [SerializeField] private float upward = 4f;
    [SerializeField] private float spawnRadius = 0.25f;

    [Header("Meteor Life")]
    [SerializeField] private float despawnAfterImpact = 0.2f;

    // ---- Network Variables (Server writes) ----
    private readonly NetworkVariable<Vector3> P0 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector3> P1 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector3> P2 = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> StartTime = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> Duration = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> Started = new(writePerm: NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> Impacted = new(writePerm: NetworkVariableWritePermission.Server);

    private Vector3 _prevPos;

    // ==============================
    // 落下ループSE
    // ==============================
    [Header("SFX")]
    [SerializeField] private string fallLoopSfxTag = "SE_Meteor_FallLoop"; // ←DBのタグ名に合わせて
    private string _fallLoopInstanceTag;
    private bool _fallLoopPlaying = false;

    public override void OnNetworkSpawn()
    {
        // 隕石ごとにユニークなタグ（複数同時でも干渉しない）
        _fallLoopInstanceTag = $"{fallLoopSfxTag}_{NetworkObjectId}";
    }

    private void OnDisable()
    {
        // 取りこぼし防止：無効化されたら止める
        StopFallLoopLocal();
    }

    private void StopFallLoopLocal()
    {
        if (!_fallLoopPlaying) return;
        if (NetworkSoundManager.Instance == null) return;

        NetworkSoundManager.Instance.StopLoopSfx(
            _fallLoopInstanceTag,
            NetworkSoundManager.SoundScope.LocalOnly
        );
        _fallLoopPlaying = false;
    }

    public void ServerSetupPath(Vector3 p0, Vector3 p1, Vector3 p2, double startTime, float duration)
    {
        if (!IsServer) return;

        P0.Value = p0;
        P1.Value = p1;
        P2.Value = p2;
        StartTime.Value = startTime;
        Duration.Value = Mathf.Max(0.01f, duration);

        Started.Value = true;
        Impacted.Value = false;

        transform.SetPositionAndRotation(p0, Quaternion.identity);
        _prevPos = p0;

        // ★追加：炎エフェクトを「隕石にアタッチ生成」
        ServerSpawnFireEffectOnce();
    }

    //エフェクト生成
    private void ServerSpawnFireEffectOnce()
    {
        if (_fireSpawnRequested) return;
        _fireSpawnRequested = true;

        if (NetworkEffectSpawner.Instance == null)
        {
            Debug.LogWarning("[MeteorManager] NetworkEffectSpawner.Instance が null です");
            return;
        }

        NetworkEffectSpawner.Instance.PlayEffectAttached(
            13,
            GetComponent<NetworkObject>(),
            transform.position,
            transform.rotation
        );

        StartCoroutine(CoResolveFireEffectTransform());
    }

    private IEnumerator CoResolveFireEffectTransform()
    {
        yield return null;

        var markers = GetComponentsInChildren<EffectIdMarker>(true);
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null && i < fireEffectId.Length && markers[i].effectId == fireEffectId[i])
            {
                _fireEffectTransform.Add(markers[i].transform);
                break;
            }
        }

        if (_fireEffectTransform == null)
        {
            Debug.LogWarning("[MeteorManager] 炎エフェクトのTransformが見つかりません（EffectIdMarkerをPrefabに付けてる？）");
        }
    }

    private void Update()
    {
        if (!Started.Value)
        {
            StopFallLoopLocal();
            return;
        }

        double now = NetworkManager.Singleton.ServerTime.Time;
        float t = (float)((now - StartTime.Value) / Duration.Value);

        // 開始前は位置固定＆落下音は鳴らさない
        if (t <= 0f)
        {
            transform.position = P0.Value;
            _prevPos = transform.position;
            StopFallLoopLocal();
            return;
        }

        float tt = Mathf.Clamp01(t);

        // ==============================
        // ★落下中だけループSE（3D・追従）
        // ==============================
        bool isFalling = (tt > 0f && tt < 1f && !Impacted.Value);

        if (isFalling)
        {
            // LocalOnlyで各端末が鳴らす（毎フレAllClientsで飛ばさない）
            NetworkSoundManager.Instance.StartLoopSfx(
                _fallLoopInstanceTag,
                NetworkSoundManager.SoundScope.LocalOnly,
                true,
                transform.position
            );
            _fallLoopPlaying = true;
        }
        else
        {
            StopFallLoopLocal();
        }

        Vector3 pos = Bezier(P0.Value, P1.Value, P2.Value, tt);
        transform.position = pos;

        Vector3 vel = pos - _prevPos;

        // 隕石本体の向き
        if (vel.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

        // 炎エフェクトを進行方向に向ける
        UpdateFireFacing(vel);

        if (visualRoot != null)
            visualRoot.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        _prevPos = pos;

        if (tt >= 1f && IsServer && !Impacted.Value)
        {
            Impacted.Value = true;
            ServerOnImpact(P2.Value);

            // サーバー到達確定時にも止める（念押し）
            StopFallLoopLocal();
        }
    }

    // ============================
    // 炎の回転制御
    // ============================
    private void UpdateFireFacing(Vector3 vel)
    {
        if (_fireEffectTransform == null) return;
        if (vel.sqrMagnitude < 0.000001f) return;

        Vector3 dir = vel.normalized;

        if (fireFaceAgainstVelocity)
            dir = -dir;

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion offset = Quaternion.Euler(fireAxisOffsetEuler);
        Quaternion target = look * offset;

        foreach (Transform t in _fireEffectTransform)
        {
            if (t == null) continue;

            if (fireRotationSlerp <= 0f)
            {
                t.rotation = target;
            }
            else
            {
                t.rotation = Quaternion.Slerp(
                    t.rotation,
                    target,
                    Time.deltaTime * fireRotationSlerp
                );
            }
        }
    }

    private void ServerOnImpact(Vector3 impactPoint)
    {
        // エフェクトの再生
        NetworkEffectSpawner.Instance.PlayEffect(
            14,
            impactPoint,
            Quaternion.identity,
            new Vector3(knockbackRadius, knockbackRadius, knockbackRadius)
        );

        // 衝撃波を出す
        ServerApplyKnockback(impactPoint);
        // 金平糖をはじけさせる
        ServerSpawnKonpeito(impactPoint);

        // 少し待って隕石を消す
        Invoke(nameof(ServerDespawnSelf), despawnAfterImpact);
    }

    private void ServerSpawnKonpeito(Vector3 origin)
    {
        if (konpeitoPrefabs == null || konpeitoPrefabs.Length == 0)
        {
            Debug.LogWarning("[NetworkMeteor] konpeitoPrefabs が空");
            return;
        }

        int count = Random.Range(minKonpeito, maxKonpeito + 1);
        for (int i = 0; i < count; i++)
        {
            var prefab = konpeitoPrefabs[Random.Range(0, konpeitoPrefabs.Length)];

            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = Mathf.Abs(offset.y);

            Vector3 spawnPos = origin + offset;
            Quaternion rot = Random.rotation;

            NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
            if (_ObjectPool == null)
            {
                Debug.LogError("NetworkObjectPool: prefab is NULL");
                return;
            }

            NetworkObject obj = _ObjectPool.Get(prefab.GetComponent<NetworkObject>(), spawnPos, rot);
            obj.Spawn(true);
            obj.GetComponent<PooledNetworkObject>().SetPrefab(prefab.GetComponent<NetworkObject>());

            if (obj.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                dir = dir.normalized;

                Vector3 impulse = dir * burstSpeed + Vector3.up * upward;
                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddTorque(Random.onUnitSphere * 3f, ForceMode.Impulse);
            }
        }
    }

    private void ServerApplyKnockback(Vector3 impactPoint)
    {
        if (!IsServer) return;

        Collider[] hits = Physics.OverlapSphere(
            impactPoint,
            knockbackRadius,
            knockbackMask,
            QueryTriggerInteraction.Ignore
        );

        Debug.Log("当たったオブジェクト数 = " + hits.Length);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];

            if (col.transform.IsChildOf(transform)) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null) continue;

            rb.AddExplosionForce(
                knockbackForce,
                impactPoint,
                knockbackRadius,
                knockbackUpward,
                ForceMode.Impulse
            );

            var ice = hits[i].GetComponent<IcePillar>();
            if (ice != null)
            {
                Vector3 hitPoint = hits[i].ClosestPoint(transform.position);
                Vector3 hitDir = (hits[i].transform.position - transform.position).normalized;
                if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

                ice.ApplyDamageServer(OwnerClientId, hitPoint, hitDir, false, 9f);
            }

            if (hits[i].gameObject.CompareTag("Player"))
            {
                var mp = hits[i].gameObject.GetComponent<MovePlayerKey>();
                if (mp != null)
                {
                    mp.Stun(2f);
                    mp.PlayCameraShake();
                }
            }
        }
    }

    private void ServerDespawnSelf()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 ab = Vector3.Lerp(a, b, t);
        Vector3 bc = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(ab, bc, t);
    }
}
