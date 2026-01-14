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

        var go = other.gameObject;

        // すでに中にいるなら重複加算しない
        if (_inside.ContainsKey(go)) return;

        // 寄与分を計算
        if (!TryBuildEntry(go, out var entry)) return;

        _inside.Add(go, entry);

        // 加算
        netCount.Value += entry.countDelta;
        netScoreNow.Value += entry.scoreDelta;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (_isProcessingShipment) return; // 出荷中はExitを無視（演出で消えるので）

        var go = other.gameObject;

        if (_inside.TryGetValue(go, out var entry))
        {
            _inside.Remove(go);

            // 減算
            netCount.Value = Mathf.Max(0, netCount.Value - entry.countDelta);

            // 障害物（scoreDeltaが負）にも対応： -scoreDelta で元に戻る
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
        // 出荷開始時点の値を確定（演出中に中身が消えても壊れない）
        float shippedScore = netScoreNow.Value;

        // 出荷エフェクト
        if (NetworkEffectSpawner.Instance != null)
        {
            NetworkEffectSpawner.Instance.PlayEffect(
                _wagashiTeleportId,
                transform.position,
                transform.rotation
            );
        }

        // ゴール内の対象を消す（演出）
        // ※ここでSetActive(false)/DestroySelfされてもExitは来ないので、出荷中はCleanup/Exit無視している
        foreach (var kv in _inside)
        {
            var go = kv.Key;
            if (go == null) continue;

            if (go.TryGetComponent<PooledNetworkObject>(out var pooled))
            {
                pooled.DestroySelf();
            }
            else
            {
                // NetworkObjectや普通のGameObjectの場合は必要に応じて処理を追加
                // Debug.LogWarning($"No PooledNetworkObject on {go.name}");
            }
        }

        // 演出時間待つ
        yield return new WaitForSeconds(shrinkDelay);

        // スコア確定
        netScoreTotal.Value += shippedScore;

        // リセット
        netCount.Value = 0;
        netScoreNow.Value = 0f;
        netTimeUp.Value = 0f;

        // 追跡もクリア（次のカウントへ）
        _inside.Clear();

        // フラグ戻す
        _hasTeleportEffectPlayed = false;
        _isProcessingShipment = false;
    }
}
