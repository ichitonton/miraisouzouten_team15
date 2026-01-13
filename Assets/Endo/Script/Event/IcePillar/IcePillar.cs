using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// IcePillar（Network版）
/// - せり上がり：サーバーが位置を動かす（NetworkTransformで同期）
/// - 当たり判定：氷柱側 OnCollisionEnter / OnTriggerEnter（Punch）
///     - クライアント：ServerRpc でヒット報告
///     - サーバー：ランダムダメージ（0.5-1.25）を確定しHP減算
/// - ヒット演出：ClientRpc で VisualRoot を揺らす（見た目だけ）
/// - 破壊：サーバーで判定して Broken を NetworkVariable で配布
/// - 和菓子：柱に追従（FrozenWagashi側でスケール継承なし）
/// - 追加：HP割合に応じて VisualRoot のモデル段階を切替（NetworkVariableで同期）
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
    [Tooltip("Tagでパンチ判定する場合は設定。空ならPunchコンポーネントで判定します。")]
    [SerializeField] private string punchTag = "";

    [Header("Break Visual/Collision")]
    [SerializeField] private GameObject iceVisualObject; // 氷全体の見た目（壊れたら消す）
    [SerializeField] private Collider[] collidersToDisableOnBreak;

    [Header("Wagashi Attach")]
    [SerializeField] private BoxCollider attachVolume;
    [SerializeField] private NetworkPrefabDatabase prefabDatabase;
    [SerializeField] private Vector2Int wagashiCountRange = new(5, 6);
    [SerializeField, Min(0f)] private float wagashiReleaseImpulse = 2.0f;

    // -------------------------
    // ★追加：HP段階モデル切替
    // -------------------------
    [Header("Visual By HP (Stage Models)")]
    [Tooltip("HP段階ごとのモデル（0:健康, 1:ひび1, 2:ひび2, 3:瀕死…）を順番に入れる")]
    [SerializeField] private GameObject[] hpStageModels;

    [Tooltip("HP割合で段階が落ちる境界（例：0.66,0.33,0.10）。モデル数-1個を推奨")]
    [SerializeField] private float[] hpStageThresholds = new float[] { 0.66f, 0.33f, 0.10f };

    // 壊れたかどうかは全員で共有
    private readonly NetworkVariable<bool> _broken = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ★追加：HP段階（見た目用）
    private readonly NetworkVariable<int> _hpStage = new(
        0,
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
        // 参照が未設定なら自動補完
        if (visualRoot == null) visualRoot = transform;
        if (iceVisualObject == null && visualRoot != null) iceVisualObject = visualRoot.gameObject;

        if (attachVolume == null) attachVolume = GetComponentInChildren<BoxCollider>();

        _broken.OnValueChanged += OnBrokenChanged;

        // ★追加：段階変化を購読＆初期適用（途中参加でも正しく見える）
        _hpStage.OnValueChanged += OnHpStageChanged;
        ApplyHpStage(_hpStage.Value);

        if (IsServer)
        {
            _hp = maxHP;

            // ★追加：初期段階を配信
            UpdateHpStageServer();

            StartCoroutine(RiseRoutineServer());
        }
    }

    private void OnDestroy()
    {
        _broken.OnValueChanged -= OnBrokenChanged;
        _hpStage.OnValueChanged -= OnHpStageChanged;
    }

    // -------------------------
    // HP段階モデル切替
    // -------------------------
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

    private void UpdateHpStageServer()
    {
        if (!IsServer) return;
        if (hpStageModels == null || hpStageModels.Length == 0) return;

        float ratio = (_hp <= 0f) ? 0f : (_hp / maxHP);

        int stage = 0;

        if (hpStageThresholds != null)
        {
            // ratio <= threshold で段階を落とす（後ろほど深いダメージ段階）
            for (int i = 0; i < hpStageThresholds.Length; i++)
            {
                if (ratio <= hpStageThresholds[i]) stage = i + 1;
            }
        }

        stage = Mathf.Clamp(stage, 0, hpStageModels.Length - 1);

        if (_hpStage.Value != stage)
            _hpStage.Value = stage;
    }

    // -------------------------
    // Broken同期
    // -------------------------
    private void OnBrokenChanged(bool prev, bool next)
    {
        if (!next) return;

        // 壊れたら見た目と当たり判定を消す（クライアント含め）
        if (iceVisualObject != null) iceVisualObject.SetActive(false);

        if (collidersToDisableOnBreak != null)
        {
            foreach (var c in collidersToDisableOnBreak)
            {
                if (c != null) c.enabled = false;
            }
        }
    }

    // -------------------------
    // せり上がり（サーバー）
    // -------------------------
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

            // attachVolume 内のローカル点をランダムに作る
            Vector3 localInBox = GetRandomPointInLocalBox(attachVolume);
            Quaternion localRot = Random.rotation;

            // ワールドに変換（attachVolume）
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

            // 氷漬け状態（NetworkVariableに任せる）
            fw.SetFrozenServer(true);

            // 柱ローカルで「どこに付くか」を決める（Scaleは継承されない）
            Vector3 localPosToPillar = transform.InverseTransformPoint(worldPos);
            Quaternion localRotToPillar = Quaternion.Inverse(transform.rotation) * worldRot;

            // 追従セット（全クライアントに配る）
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

    // -------------------------
    // 当たり判定（Collision / Trigger 両対応）
    // -------------------------
    private void OnCollisionEnter(Collision collision)
    {
        if (_broken.Value) return;

        var col = collision.collider;
        if (!IsPunchHit(col)) return;

        Vector3 hitDir = collision.relativeVelocity.sqrMagnitude > 0.001f
            ? collision.relativeVelocity.normalized
            : (col.transform.position - transform.position).normalized;

        Vector3 hitPoint = collision.GetContact(0).point;

        HandlePunchHit(col, hitPoint, hitDir);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_broken.Value) return;

        if (!IsPunchHit(other)) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitDir = (other.transform.position - transform.position).normalized;
        if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;

        HandlePunchHit(other, hitPoint, hitDir);
    }

    private void HandlePunchHit(Collider punchCollider, Vector3 hitPoint, Vector3 hitDir)
    {
        // クライアントは「自分の拳」だけ報告（他人の拳で送らない）
        if (!IsServer)
        {
            if (!IsLocalPlayersPunch(punchCollider)) return;
            ReportPunchHitServerRpc(hitPoint, hitDir);
            return;
        }

        // サーバーは直接確定
        ulong attackerId = GetOwnerClientIdFromPunch(punchCollider);
        ApplyDamageServer(attackerId, hitPoint, hitDir);
    }

    private bool IsPunchHit(Collider col)
    {
        // Tag優先
        if (!string.IsNullOrEmpty(punchTag))
        {
            if (col.CompareTag(punchTag)) return true;
        }

        // コンポーネント判定（Punchが拳側に付いている前提）
        return col.GetComponentInParent<Punch>() != null;
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
        if (ownerNO != null) return ownerNO.OwnerClientId;

        return 0;
    }

    // -------------------------
    // サーバー確定処理
    // -------------------------
    [ServerRpc(RequireOwnership = false)]
    private void ReportPunchHitServerRpc(Vector3 hitPoint, Vector3 hitDir, ServerRpcParams rpcParams = default)
    {
        if (_broken.Value) return;

        ulong attackerId = rpcParams.Receive.SenderClientId;
        ApplyDamageServer(attackerId, hitPoint, hitDir);
    }

    private void ApplyDamageServer(ulong attackerClientId, Vector3 hitPoint, Vector3 hitDir)
    {
        if (!IsServer) return;
        if (_broken.Value) return;

        // 多段ヒット抑制
        float now = Time.time;
        if (_lastHitTime.TryGetValue(attackerClientId, out var last))
        {
            if (now - last < hitCooldown) return;
        }
        _lastHitTime[attackerClientId] = now;

        // ランダムダメージ（サーバーで決定）
        float damage = Random.Range(damageMin, damageMax);

        // ダメージに応じて揺れ強さも少し変える（任意）
        float amp = Mathf.Lerp(0.06f, 0.14f, Mathf.InverseLerp(damageMin, damageMax, damage));
        PlayHitShakeClientRpc(amp, hitShakeDuration, hitShakeFrequency);

        // HP減算
        _hp -= damage;
        if (_hp < 0f) _hp = 0f;

        // ★追加：HP段階更新（見た目切替）
        UpdateHpStageServer();

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

        // 和菓子解放
        ReleaseWagashiServer(hitDir);

        //氷柱が壊れたエフェクトを出す
        Vector3 pos = transform.position;

        //↑
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x, pos.y + 2f, pos.z), Quaternion.identity, new Vector3(2f, 2f, 2f));
        //真ん中
        NetworkEffectSpawner.Instance.PlayEffect(11, new Vector3(pos.x, pos.y + 1f, pos.z), Quaternion.identity, new Vector3(2f, 2f, 2f));
        //↓
        NetworkEffectSpawner.Instance.PlayEffect(11, pos, Quaternion.identity, new Vector3(2f, 2f, 2f));

        // しばらくして柱だけ消す
        StartCoroutine(DespawnAfterSecondsServer(0.1f));
    }

    private void ReleaseWagashiServer(Vector3 hitDir)
    {
        for (int i = 0; i < _spawnedWagashi.Count; i++)
        {
            var w = _spawnedWagashi[i];
            if (w == null) continue;

            // 凍結解除（FrozenWagashi側でNetworkTransform復帰まで面倒見てる想定）
            w.SetFrozenServer(false);

            // 物理がある場合だけ少し飛ばす
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
