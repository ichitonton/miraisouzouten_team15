using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// IcePillar（Network）
/// - Serverがせり上がり（NetworkTransform同期）
/// - Hit：ClientはServerRpcで報告、ServerはApplyDamageServerで確定
/// - ApplyDamageServer(dmg)：dmg<=0ならランダム、>0なら指定ダメ
/// - 揺れClientRpcはApplyDamageServer内から呼ぶ
/// - Wagashi：attachVolume内でランダム生成（重なり防止）→ 親子固定
/// - Break：和菓子解放＋IceBlock散布＋柱Despawn
/// - ループエフェクト（サーバーのみ）
/// - ★追加：HP割合に応じてモデル段階を切替（NetworkVariableで同期）
/// </summary>
public class IcePillar : NetworkBehaviour
{
    [Header("Rise (Server drives position)")]
    [SerializeField, Min(0f)] private float buryDepth = 2.0f;
    [SerializeField, Min(0f)] private float riseHeight = 3.0f;
    [SerializeField, Min(0.01f)] private float riseDuration = 1.2f;

    [Header("Visual Root (Shake this, NOT network root)")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject iceVisualObject;

    [Header("Hit Shake (Client Visual)")]
    [SerializeField, Min(0f)] private float hitShakePosAmp = 0.08f;
    [SerializeField, Min(0f)] private float hitShakeRotAmp = 6.0f;   // degrees
    [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float hitShakeFrequency = 22f;

    [Header("HP (Server)")]
    [SerializeField, Min(1f)] private float maxHP = 60f;

    [Header("Damage (Server decides)")]
    [SerializeField, Min(0f)] private float damageMin = 0.5f;
    [SerializeField, Min(0f)] private float damageMax = 1.25f;

    [Header("Hit Control")]
    [SerializeField, Min(0f)] private float hitCooldown = 0.08f;
    [Tooltip("Tagでパンチ判定する場合は設定。空ならPunchコンポーネントで判定します。")]
    [SerializeField] private string punchTag = "";

    [Header("Wagashi Attach")]
    [SerializeField] private BoxCollider attachVolume;
    [SerializeField] private NetworkPrefabDatabase prefabDatabase;
    [SerializeField] private Vector2Int wagashiCountRange = new(5, 6);
    [SerializeField, Min(0f)] private float wagashiMinSeparation = 0.25f;
    [SerializeField, Range(1, 50)] private int wagashiPlacementTries = 20;
    [SerializeField, Min(0f)] private float wagashiReleaseImpulse = 2.0f;

    [Header("Ice Debris (IceBlock)")]
    [SerializeField] private NetworkObject iceBlockPrefab;
    [SerializeField] private Vector2Int iceBlockCountRange = new(5, 6);
    [SerializeField, Min(0f)] private float iceBlockSpawnRadius = 0.6f;
    [SerializeField, Min(0f)] private float iceBlockImpulseMin = 2.0f;
    [SerializeField, Min(0f)] private float iceBlockImpulseMax = 4.0f;
    [SerializeField, Min(0f)] private float iceBlockUpBias = 0.8f;
    [SerializeField, Min(0f)] private float iceBlockRandomTorque = 12f;
    [SerializeField, Min(0f)] private float iceBlockLifeSeconds = 4.0f;

    [Header("Break")]
    [SerializeField] private Collider[] collidersToDisableOnBreak;
    [SerializeField, Min(0f)] private float despawnDelay = 0.1f;

    // ★ループエフェクト
    [Header("Loop Effect (Server)")]
    [SerializeField] private bool enableLoopEffect = true;
    [SerializeField] private int loopEffectId = 12;
    [SerializeField, Min(0.01f)] private float _interval = 1.0f;
    [SerializeField] private Vector3 loopEffectOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector3 loopEffectScale = new Vector3(1.8f, 1.8f, 1.8f);
    private Coroutine _loopCo;

    // ★HP段階モデル切替
    [Header("Visual By HP (Stage Models)")]
    [Tooltip("0:健康 → 1:ひび1 → 2:ひび2 → ... の順で入れる")]
    [SerializeField] private GameObject[] hpStageModels;

    [Tooltip("HP割合で段階が落ちる境界。例：0.66,0.33,0.10（モデル数-1個推奨）")]
    [SerializeField] private float[] hpStageThresholds = new float[] { 0.66f, 0.33f, 0.10f };

    private readonly NetworkVariable<bool> _broken = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ★段階同期（途中参加でも一致）
    private readonly NetworkVariable<int> _hpStage = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float _hp;
    private Vector3 _basePos;

    private readonly Dictionary<ulong, float> _lastHitTime = new();
    private readonly List<FrozenWagashi> _spawnedWagashi = new();

    private Coroutine _shakeCo;

    public override void OnNetworkSpawn()
    {
        if (visualRoot == null)
        {
            Debug.LogWarning("[IcePillar] visualRoot 未設定。Prefabで見た目だけの子(VisualRoot)を指定して！", this);
            visualRoot = transform;
        }

        if (iceVisualObject == null && visualRoot != null)
            iceVisualObject = visualRoot.gameObject;

        if (attachVolume == null)
            attachVolume = GetComponentInChildren<BoxCollider>();

        _broken.OnValueChanged += OnBrokenChanged;

        // ★段階購読＆初期適用（途中参加でも正しい見た目）
        _hpStage.OnValueChanged += OnHpStageChanged;
        ApplyHpStage(_hpStage.Value);

        if (IsServer)
        {
            _hp = maxHP;

            // ★初期段階を配信
            UpdateHpStageServer(Vector3.zero);
            StartCoroutine(RiseRoutineServer());
            Vector3 pos = transform.position + loopEffectOffset;
            //if (enableLoopEffect) NetworkEffectSpawner.Instance.PlayEffect(loopEffectId, pos, Quaternion.identity, loopEffectScale);
            //_loopCo = StartCoroutine(Loop());
        }
    }

    private void OnDisable()
    {
        if (_loopCo != null)
        {
            StopCoroutine(_loopCo);
            _loopCo = null;
        }
    }

    private void OnDestroy()
    {
        _broken.OnValueChanged -= OnBrokenChanged;
        _hpStage.OnValueChanged -= OnHpStageChanged;
    }

    private void OnHpStageChanged(int prev, int next)
    {
        ApplyHpStage(next);
    }

    private void ApplyHpStage(int stage)
    {
        if (hpStageModels == null || hpStageModels.Length == 0) return;

        stage = Mathf.Clamp(stage, 0, hpStageModels.Length - 1);

        for (int i = 0; i < hpStageModels.Length; i++)
        {
            var go = hpStageModels[i];
            if (go == null) continue;
            go.SetActive(i == stage);
        }
    }

    private void UpdateHpStageServer(Vector3 hitDir)
    {
        if (!IsServer) return;
        if (hpStageModels == null || hpStageModels.Length == 0) return;

        float ratio = (_hp <= 0f) ? 0f : (_hp / maxHP);

        int stage = 0;
        if (hpStageThresholds != null)
        {
            // ratio <= threshold で段階を落とす（後ろほど深いダメージ）
            for (int i = 0; i < hpStageThresholds.Length; i++)
            {
                if (ratio <= hpStageThresholds[i]) stage = i + 1;
            }
        }

        stage = Mathf.Clamp(stage, 0, hpStageModels.Length - 1);

        if (_hpStage.Value != stage)
        {
            _hpStage.Value = stage;
            SpawnIceBlocksServer(hitDir);
        }
    }

    private void OnBrokenChanged(bool prev, bool next)
    {
        if (!next) return;

        if (_loopCo != null)
        {
            StopCoroutine(_loopCo);
            _loopCo = null;
        }

        if (iceVisualObject != null) iceVisualObject.SetActive(false);

        if (collidersToDisableOnBreak != null)
        {
            foreach (var c in collidersToDisableOnBreak)
                if (c != null) c.enabled = false;
        }
    }

    private IEnumerator Loop()
    {
        var wait = new WaitForSeconds(_interval);
        while (!_broken.Value)
        {
            if (NetworkEffectSpawner.Instance != null)
            {
                Vector3 pos = transform.position + loopEffectOffset;
                NetworkEffectSpawner.Instance.PlayEffect(loopEffectId, pos, Quaternion.identity, loopEffectScale);
            }
            yield return wait;
        }
    }

    private IEnumerator RiseRoutineServer()
    {
        if (!IsServer) yield break;

        _basePos = transform.position;
        transform.position = _basePos + Vector3.down * buryDepth;

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
            Debug.LogWarning("[IcePillar] attachVolume が未設定です", this);
            return;
        }

        if (prefabDatabase == null || prefabDatabase._entries == null || prefabDatabase._entries.Count == 0)
        {
            Debug.LogWarning("[IcePillar] prefabDatabase が空です", this);
            return;
        }

        _spawnedWagashi.Clear();

        int count = Random.Range(wagashiCountRange.x, wagashiCountRange.y + 1);
        var placedWorldPos = new List<Vector3>(count);

        for (int i = 0; i < count; i++)
        {
            var entry = prefabDatabase._entries[Random.Range(0, prefabDatabase._entries.Count)];
            var prefab = entry._prefab;
            if (prefab == null) continue;

            Vector3 worldPos = FindNonOverlappingPointInVolume(
                attachVolume, placedWorldPos, wagashiMinSeparation, wagashiPlacementTries
            );
            placedWorldPos.Add(worldPos);

            Quaternion worldRot = Random.rotation;

            var go = Instantiate(prefab, worldPos, worldRot);

            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogWarning("[IcePillar] 生成した和菓子に NetworkObject がありません", this);
                Destroy(go);
                continue;
            }

            no.Spawn(true);

            var fw = go.GetComponent<FrozenWagashi>();
            if (fw == null)
            {
                Debug.LogWarning("[IcePillar] 生成した和菓子に FrozenWagashi がありません", this);
                continue;
            }

            Vector3 localPosToPillar = transform.InverseTransformPoint(worldPos);
            Quaternion localRotToPillar = Quaternion.Inverse(transform.rotation) * worldRot;

            fw.AttachToPillarServer(NetworkObject, localPosToPillar, localRotToPillar);
            _spawnedWagashi.Add(fw);
        }
    }

    private static Vector3 FindNonOverlappingPointInVolume(BoxCollider box, List<Vector3> existing, float minDist, int tries)
    {
        if (minDist <= 0f || existing == null || existing.Count == 0)
            return GetRandomPointInBoxWorld(box);

        Vector3 best = GetRandomPointInBoxWorld(box);
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < tries; i++)
        {
            Vector3 p = GetRandomPointInBoxWorld(box);

            bool ok = true;
            for (int j = 0; j < existing.Count; j++)
            {
                if ((existing[j] - p).sqrMagnitude < minDistSqr)
                {
                    ok = false;
                    break;
                }
            }

            if (ok) return p;
            best = p;
        }

        return best;
    }

    private static Vector3 GetRandomPointInBoxWorld(BoxCollider box)
    {
        Vector3 half = box.size * 0.5f;

        float x = Random.Range(-half.x, half.x);
        float y = Random.Range(-half.y, half.y);
        float z = Random.Range(-half.z, half.z);

        Vector3 local = box.center + new Vector3(x, y, z);
        return box.transform.TransformPoint(local);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_broken.Value) return;

        var col = collision.collider;
        if (!IsPunchHit(col)) return;

        Vector3 hitPoint = collision.GetContact(0).point;

        Vector3 hitDir = collision.relativeVelocity.sqrMagnitude > 0.001f
            ? collision.relativeVelocity.normalized
            : (col.transform.position - transform.position).normalized;

        if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

        bool star = collision.gameObject.GetComponentInParent<MovePlayerKey>().GetUseStar();
        HandlePunchHit(col, hitPoint, hitDir, star);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_broken.Value) return;
        if (!IsPunchHit(other)) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitDir = (other.transform.position - transform.position).normalized;
        if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

        bool star = other.gameObject.GetComponentInParent<MovePlayerKey>().GetUseStar();
        HandlePunchHit(other, hitPoint, hitDir, star);
    }

    private bool IsPunchHit(Collider col)
    {
        if (!string.IsNullOrEmpty(punchTag) && col.CompareTag(punchTag))
            return true;

        return col.GetComponentInParent<Punch>() != null;
    }

    private void HandlePunchHit(Collider punchCollider, Vector3 hitPoint, Vector3 hitDir, bool star)
    {
        if (!IsServer)
        {
            if (!IsLocalPlayersPunch(punchCollider)) return;
            ReportPunchHitServerRpc(hitPoint, hitDir, star);
            return;
        }

        ulong attackerId = GetOwnerClientIdFromPunch(punchCollider);
        ApplyDamageServer(attackerId, hitPoint, hitDir, star, 0f);
    }

    private bool IsLocalPlayersPunch(Collider col)
    {
        if (NetworkManager.Singleton == null) return false;

        var ownerNO = col.GetComponentInParent<NetworkObject>();
        if (ownerNO == null) return false;

        return ownerNO.OwnerClientId == NetworkManager.Singleton.LocalClientId;
    }

    private ulong GetOwnerClientIdFromPunch(Collider col)
    {
        var ownerNO = col.GetComponentInParent<NetworkObject>();
        return ownerNO != null ? ownerNO.OwnerClientId : 0;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReportPunchHitServerRpc(Vector3 hitPoint, Vector3 hitDir, bool star, ServerRpcParams rpcParams = default)
    {
        if (_broken.Value) return;

        ulong attackerId = rpcParams.Receive.SenderClientId;
        ApplyDamageServer(attackerId, hitPoint, hitDir, star, 0f);
    }

    public void ApplyDamageServer(ulong attackerClientId, Vector3 hitPoint, Vector3 hitDir, bool star, float dmg)
    {
        if (!IsServer) return;
        if (_broken.Value) return;

        float now = Time.time;
        if (_lastHitTime.TryGetValue(attackerClientId, out var last) && (now - last < hitCooldown))
            return;
        _lastHitTime[attackerClientId] = now;

        float damage = (dmg <= 0f) ? Random.Range(damageMin, damageMax) : dmg;
        if (star) damage = maxHP;

        _hp -= damage;
        if (_hp < 0f) _hp = 0f;

        // ★HP段階更新（見た目切替）
        UpdateHpStageServer(hitDir);

        float t = Mathf.InverseLerp(damageMin, damageMax, Mathf.Clamp(damage, damageMin, damageMax));
        float ampPos = Mathf.Lerp(hitShakePosAmp * 0.7f, hitShakePosAmp * 1.4f, t);
        float ampRot = Mathf.Lerp(hitShakeRotAmp * 0.7f, hitShakeRotAmp * 1.4f, t);
        PlayHitShakeClientRpc(ampPos, ampRot, hitShakeDuration, hitShakeFrequency);

        if (_hp <= 0f)
            BreakServer(hitDir);
    }

    [ClientRpc]
    private void PlayHitShakeClientRpc(float posAmp, float rotAmpDeg, float duration, float frequency)
    {
        if (visualRoot == null) return;

        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(HitShakeRoutineLocal(visualRoot, posAmp, rotAmpDeg, duration, frequency));
    }

    private IEnumerator HitShakeRoutineLocal(Transform target, float posAmp, float rotAmpDeg, float duration, float frequency)
    {
        Vector3 basePos = target.localPosition;
        Quaternion baseRot = target.localRotation;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / duration);
            float damp = 1f - n;

            float time = Time.time * frequency;
            float nx = (Mathf.PerlinNoise(time, 0.1f) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(0.2f, time) - 0.5f) * 2f;

            target.localPosition = basePos + new Vector3(nx, 0f, nz) * posAmp * damp;

            float rx = nz * rotAmpDeg * damp;
            float rz = nx * rotAmpDeg * damp;
            target.localRotation = baseRot * Quaternion.Euler(rx, 0f, rz);

            yield return null;
        }

        target.localPosition = basePos;
        target.localRotation = baseRot;
        _shakeCo = null;
    }

    private void BreakServer(Vector3 hitDir)
    {
        if (!IsServer) return;
        if (_broken.Value) return;

        _broken.Value = true;

        NetworkSoundManager.Instance.PlaySfx("Event_IceBreak", NetworkSoundManager.SoundScope.AllClients, true, transform.position);

        Vector3 pos = transform.position + loopEffectOffset;
        NetworkEffectSpawner.Instance.PlayEffect(11, pos, Quaternion.identity, new Vector3(2.3f,2.3f,2.3f));
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x,pos.y + 1f,pos.z), Quaternion.identity, new Vector3(2.3f, 2.3f, 2.3f));
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x, pos.y + 2f, pos.z), Quaternion.identity, new Vector3(2.3f, 2.3f, 2.3f));

        


        SpawnIceBlocksServer(hitDir);
        ReleaseWagashiServer(hitDir);

        StartCoroutine(DespawnAfterSecondsServer(despawnDelay));
    }

    private void ReleaseWagashiServer(Vector3 hitDir)
    {
        for (int i = 0; i < _spawnedWagashi.Count; i++)
        {
            var w = _spawnedWagashi[i];
            if (w == null) continue;

            w.DetachServer();

            var rb = w.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddForce(hitDir * wagashiReleaseImpulse, ForceMode.Impulse);
        }

        _spawnedWagashi.Clear();
    }

    private IEnumerator DespawnAfterSecondsServer(float sec)
    {
        yield return new WaitForSeconds(sec);

        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void SpawnIceBlocksServer(Vector3 hitDir)
    {
        if (!IsServer) return;
        if (iceBlockPrefab == null) return;

        int count = Random.Range(iceBlockCountRange.x, iceBlockCountRange.y + 1);
        Vector3 center = (visualRoot != null) ? visualRoot.position : transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 rand = Random.insideUnitSphere * iceBlockSpawnRadius;
            rand.y = Mathf.Abs(rand.y);

            center.y += 3f;
            Vector3 spawnPos = center + rand;

            var debris = Instantiate(iceBlockPrefab, spawnPos, Random.rotation);
            debris.Spawn(true);

            var rb = debris.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (hitDir.sqrMagnitude > 0.001f ? hitDir.normalized : Vector3.forward);
                dir += Random.onUnitSphere * 0.45f;
                dir.y = Mathf.Abs(dir.y) + iceBlockUpBias;
                dir.Normalize();

                float impulse = Random.Range(iceBlockImpulseMin, iceBlockImpulseMax);
                rb.AddForce(dir * impulse, ForceMode.Impulse);

                rb.AddTorque(Random.onUnitSphere * iceBlockRandomTorque, ForceMode.Impulse);
            }

            //if (iceBlockLifeSeconds > 0.01f)
                //StartCoroutine(DespawnAfterSecondsServer(debris, iceBlockLifeSeconds));
        }
    }

    private IEnumerator DespawnAfterSecondsServer(NetworkObject target, float sec)
    {
        yield return new WaitForSeconds(sec);

        if (!IsServer) yield break;
        if (target == null) yield break;

        if (target.IsSpawned) target.Despawn(true);
    }
}
