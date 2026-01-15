using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GoalToUI : NetworkBehaviour
{
    // ====== 設定：このゴールは誰のもの？ ======
    [Header("Network Settings")]
    [SerializeField] public ulong targetPlayerId = 0; // 0=Host, 1=2P, 2=3P...

    // ====== ルール設定 ======
    [Header("カウント個数")]
    [SerializeField] private int countMax = 8;
    [SerializeField] private float TimeupMAX = 7.0f;

    // 転送エフェクト関連（ネットワークエフェクト）
    [Header("Teleport Effect")]
    [SerializeField] private int _TeleportId = 9;
    [SerializeField] private int _wagashiTeleportId = 8;

    [SerializeField] private float shrinkDelay = 0.6f; // 和菓子が縮む演出の時間

    private bool _isProcessingShipment = false;
    private bool _hasTeleportEffectPlayed = false;

    // ====== 同期する変数 (NetworkVariable) ======
    private NetworkVariable<int> netCount = new NetworkVariable<int>(0);
    private NetworkVariable<float> netScoreNow = new NetworkVariable<float>(0f);
    private NetworkVariable<float> netScoreTotal = new NetworkVariable<float>(0f);
    private NetworkVariable<float> netTimeUp = new NetworkVariable<float>(0f);

    // ランキング参照用（外部から呼ぶときはこれ）
    public float Score => netScoreTotal.Value;

    // ====== 和菓子カウント用Sprite ======
    [SerializeField] private GameObject[] countObjects;

    // 検索用タグ
    private const string TagSweets = "Sweets";
    private const string TagObstacles = "Obstacles";

    [SerializeField] private SpawnManager spawnManager; // ※今は使ってないなら消してOK

    public System.Action OnScoreChanged;

    // -----------------------------
    // 追加：Trigger内にいるオブジェクトの寄与を記録
    // -----------------------------
    private struct Entry
    {
        public int countDelta;
        public float scoreDelta;
    }

    // “今ゴール内にいる” を管理（Destroy/SetActive(false) でも Updateで掃除して減算できる）
    private readonly Dictionary<GameObject, Entry> _inside = new();

    // Update掃除用（GC削減）
    private readonly List<GameObject> _tmpKeys = new(64);

    public override void OnNetworkSpawn()
    {
        // スコア通知（必要なら）
        netScoreTotal.OnValueChanged += (prev, curr) => { OnScoreChanged?.Invoke(); };

        bool isMyGoal = NetworkManager.Singleton.LocalClientId == targetPlayerId;
        if (!isMyGoal) return;

        // UI更新購読
        netScoreNow.OnValueChanged += (_, __) => PushToUI();
        netScoreTotal.OnValueChanged += (_, __) => PushToUI();
        netTimeUp.OnValueChanged += (_, __) => PushToUI();

        // 初期表示
        PushToUI();

        netCount.OnValueChanged += (_, __) => UpdateActiveObject();
        UpdateActiveObject();
    }

    // ★UIに情報を送る専用の関数
    private void PushToUI()
    {
        if (NetworkManager.Singleton.LocalClientId != targetPlayerId) return;

        if (MyTeamScore_UI.Instance != null)
        {
            MyTeamScore_UI.Instance.UpdateDisplay(
                netScoreNow.Value,
                netScoreTotal.Value,
                netTimeUp.Value
            );
        }
    }

    private void Update()
    {
        // ★ゲームロジックは「サーバー」だけが動かす
        if (!IsServer) return;

        // ★重要：ゴール内で SetActive(false) / Destroy / Despawn されたものを検知して減算
        CleanupDeactivatedInside();

        // 必要個数そろっている → カウント進行
        if (netCount.Value >= countMax)
        {
            // エフェクト（最初の一回だけ）
            if (!_hasTeleportEffectPlayed)
            {
                _hasTeleportEffectPlayed = true;

                if (NetworkEffectSpawner.Instance != null)
                {
                    NetworkEffectSpawner.Instance.PlayEffect(
                        _TeleportId,
                        transform.position,
                        transform.rotation
                    );
                }
            }

            netTimeUp.Value += Time.deltaTime;

            // MAX 到達で出荷
            if (netTimeUp.Value >= TimeupMAX && !_isProcessingShipment)
            {
                _isProcessingShipment = true;
                StartCoroutine(HandleShipment());
            }
        }
        else
        {
            // そろっていない → 巻き戻し
            if (netTimeUp.Value > 0f)
            {
                _hasTeleportEffectPlayed = false;

                netTimeUp.Value -= Time.deltaTime;
                if (netTimeUp.Value < 0f) netTimeUp.Value = 0f;
            }
        }
    }

    // -----------------------------
    // Trigger入退場（サーバーのみ）
    // -----------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag(TagSweets) && !other.CompareTag(TagObstacles)) return;

        // ★子Colliderでも、親Rootを追跡対象にする
        var root = ResolveRootObject(other.gameObject);
        if (root == null) return;

        // すでに入ってるなら無視（重複加算防止）
        if (_inside.ContainsKey(root)) return;

        // 寄与分を計算（Rootで取る）
        if (!TryBuildEntry(root, out var entry)) return;

        _inside.Add(root, entry);

        netCount.Value += entry.countDelta;
        netScoreNow.Value += entry.scoreDelta;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (_isProcessingShipment) return;

        var root = ResolveRootObject(other.gameObject);
        if (root == null) return;

        if (_inside.TryGetValue(root, out var entry))
        {
            _inside.Remove(root);

            netCount.Value = Mathf.Max(0, netCount.Value - entry.countDelta);
            netScoreNow.Value = Mathf.Max(0f, netScoreNow.Value - entry.scoreDelta);
        }
    }

    // 寄与分の作成（和菓子 or 障害物）
    private bool TryBuildEntry(GameObject go, out Entry entry)
    {
        entry = default;

        if (go.TryGetComponent<JapaneseSweets_Manager>(out var sweet))
        {
            entry.countDelta = 1;
            entry.scoreDelta = sweet.GetWeight(); // +点
            return true;
        }

        if (go.TryGetComponent<obstacles_Manager>(out var obs))
        {
            entry.countDelta = obs.GetPeaces();
            entry.scoreDelta = -obs.GetWeight(); // -点
            return true;
        }

        return false;
    }

    // -----------------------------
    // SetActive(false)/Destroy/Despawn 対策：Update掃除で確実に減算
    // -----------------------------
    private void CleanupDeactivatedInside()
    {
        if (_isProcessingShipment) return;
        if (_inside.Count == 0) return;

        _tmpKeys.Clear();
        _tmpKeys.AddRange(_inside.Keys);

        for (int i = 0; i < _tmpKeys.Count; i++)
        {
            var go = _tmpKeys[i];

            // Destroy/Despawn/非アクティブ化
            if (go == null || !go.activeInHierarchy)
            {
                if (_inside.TryGetValue(go, out var entry))
                {
                    _inside.Remove(go);

                    netCount.Value = Mathf.Max(0, netCount.Value - entry.countDelta);
                    netScoreNow.Value = Mathf.Max(0f, netScoreNow.Value - entry.scoreDelta);
                }
            }
        }
    }

    // --- オブジェクト表示更新（全員に見える） ---
    private void UpdateActiveObject()
    {
        int currentCount = netCount.Value;
        for (int i = 0; i < countObjects.Length; i++)
        {
            if (countObjects[i] != null)
            {
                bool shouldBeActive = (i < currentCount);
                if (countObjects[i].activeSelf != shouldBeActive)
                    countObjects[i].SetActive(shouldBeActive);
            }
        }
    }

    // -----------------------------
    // 出荷処理（サーバーのみ）
    // -----------------------------
    private IEnumerator HandleShipment()
    {
        float shippedScore = netScoreNow.Value;

        if (NetworkEffectSpawner.Instance != null)
        {
            NetworkEffectSpawner.Instance.PlayEffect(
                _wagashiTeleportId,
                transform.position,
                transform.rotation
            );
        }

        // ★辞書キーをコピーしてから壊す（安全）
        _tmpKeys.Clear();
        _tmpKeys.AddRange(_inside.Keys);

        for (int i = 0; i < _tmpKeys.Count; i++)
        {
            var root = _tmpKeys[i];
            if (root == null) continue;

            // RootにPooledがいるなら確実にDestroySelf
            var pooled = root.GetComponent<PooledNetworkObject>();
            if (pooled != null)
            {
                pooled.DestroySelf();
                continue;
            }

            // Pooledが無いなら NetworkObject をDespawn（必要なら）
            var netObj = root.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
                continue;
            }

            // 最後の保険（普通のGameObject）
            Destroy(root);
        }

        yield return new WaitForSeconds(shrinkDelay);

        netScoreTotal.Value += shippedScore;

        netCount.Value = 0;
        netScoreNow.Value = 0f;
        netTimeUp.Value = 0f;

        _inside.Clear();

        _hasTeleportEffectPlayed = false;
        _isProcessingShipment = false;
    }

    private GameObject ResolveRootObject(GameObject hit)
    {
        if (hit == null) return null;

        // PooledNetworkObjectが親にいるなら、そこが「破壊すべき本体」
        var pooled = hit.GetComponentInParent<PooledNetworkObject>(true);
        if (pooled != null) return pooled.gameObject;

        // Pooledが無いなら、スイーツ本体（親）を拾う
        var sweet = hit.GetComponentInParent<JapaneseSweets_Manager>(true);
        if (sweet != null) return sweet.gameObject;

        var obs = hit.GetComponentInParent<obstacles_Manager>(true);
        if (obs != null) return obs.gameObject;

        // それも無いなら当たった物を返す（保険）
        return hit;
    }

}
