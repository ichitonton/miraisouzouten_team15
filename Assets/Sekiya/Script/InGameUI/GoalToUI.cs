using UnityEngine;
using TMPro;
using UnityEngine.UI;
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

    // ====== 同期する変数 (NetworkVariable) ======
    // サーバーが計算して、全員に自動で通知される変数たち
    private NetworkVariable<int> netCount = new NetworkVariable<int>(0);
    private NetworkVariable<float> netScoreNow = new NetworkVariable<float>(0f);
    private NetworkVariable<float> netScoreTotal = new NetworkVariable<float>(0f);
    private NetworkVariable<float> netTimeUp = new NetworkVariable<float>(0f);

    // ランキング参照用（外部から呼ぶときはこれ）
    public float Score { get { return netScoreTotal.Value; } }

    // ====== 和菓子カウント用Sprite ======
    [SerializeField] private GameObject[] countObjects;

    // 検索用タグ
    private const string TagSweets = "Sweets";
    private const string TagObstacles = "Obstacles";

    // サーバー側でのみ使用するリスト
    private List<GameObject> list = new List<GameObject>();

    [SerializeField] private SpawnManager spawnManager;

    public override void OnNetworkSpawn()
    {
        // 1. 自分がこのゴールの担当者かチェック
        bool isMyGoal = (NetworkManager.Singleton.LocalClientId == targetPlayerId);

        // 2. 自分の担当ゴールの場合だけ、UIマネージャーと連携する
        if (isMyGoal)
        {
            // UIの初期設定
            if (MyTeamScore_UI.Instance != null)
            {
                MyTeamScore_UI.Instance.InitSlider(TimeupMAX);
            }

            // 値が変わった時の通知先を「UIマネージャー」にする
            netScoreNow.OnValueChanged += (prev, curr) => PushToUI();
            netScoreTotal.OnValueChanged += (prev, curr) => PushToUI();
            netTimeUp.OnValueChanged += (prev, curr) => PushToUI();

            // 初回表示
            PushToUI();

            netCount.OnValueChanged += (prev, current) => UpdateActiveObject();
            UpdateActiveObject();
        }
    }

    // ★UIに情報を送る専用の関数
    private void PushToUI()
    {
        // 自分のゴールじゃないならUIには何もしない（念の為）
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

        // 必要個数そろっている → カウント進行
        if (netCount.Value >= countMax)
        {
            netTimeUp.Value += Time.deltaTime;

            // MAX 到達で出荷
            if (netTimeUp.Value >= TimeupMAX)
            {
                netScoreTotal.Value += netScoreNow.Value; // スコア確定

                // リセット処理
                netCount.Value = 0;
                netScoreNow.Value = 0f;
                netTimeUp.Value = 0.0f;

                // 出荷処理（リストにあるものを消す）
                ShipmentObjects();
            }
        }
        else
        {
            // そろっていない → 巻き戻し
            if (netTimeUp.Value > 0f)
            {
                netTimeUp.Value -= Time.deltaTime;
                if (netTimeUp.Value < 0f) netTimeUp.Value = 0f;
            }
        }
    }

    // ★出荷処理（サーバーのみ実行）
    private void ShipmentObjects()
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var go = list[i];
            if (go == null) { list.RemoveAt(i); continue; }

            // ※ここでSpawnManager経由でDestroyする
            // SpawnManager側もNetworkObject.Despawn()を使っている前提
            if (go.TryGetComponent<JapaneseSweets_Manager>(out var sweet))
            {
                spawnManager.DestroySweets(go);
                sweet.SetReset();
            }
            if (go.TryGetComponent<obstacles_Manager>(out var obs))
            {
                obs.SetReset();
            }
        }
        list.Clear();
    }

    // --- 衝突判定（サーバーのみ実行） ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // ★重要：サーバー以外は無視

        if (!other.CompareTag(TagSweets) && !other.CompareTag(TagObstacles)) return;
        if (!list.Contains(other.gameObject)) list.Add(other.gameObject);

        // NetworkVariableを書き換える（クライアントには自動で通知される）
        if (other.TryGetComponent<JapaneseSweets_Manager>(out var sweet))
        {
            netCount.Value += 1;
            netScoreNow.Value += sweet.GetWeight();
        }
        if (other.TryGetComponent<obstacles_Manager>(out var obstracles_obj))
        {
            netCount.Value += obstracles_obj.GetPeaces();
            netScoreNow.Value -= obstracles_obj.GetWeight();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return; // ★重要

        if (!other.CompareTag(TagSweets) && !other.CompareTag(TagObstacles)) return;
        list.Remove(other.gameObject);

        if (other.TryGetComponent<JapaneseSweets_Manager>(out var sweet))
        {
            netCount.Value = Mathf.Max(0, netCount.Value - 1);
            netScoreNow.Value = Mathf.Max(0f, netScoreNow.Value - sweet.GetWeight());
        }
        if (other.TryGetComponent<obstacles_Manager>(out var obstracles_obj))
        {
            netCount.Value = Mathf.Max(0, netCount.Value - obstracles_obj.GetPeaces());
            netScoreNow.Value = Mathf.Max(0f, netScoreNow.Value - obstracles_obj.GetWeight());
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
                // 状態が違うときだけSetActive呼ぶ（負荷軽減）
                if (countObjects[i].activeSelf != shouldBeActive)
                    countObjects[i].SetActive(shouldBeActive);
            }
        }
    }
}