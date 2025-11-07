using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

// ... (クラスの概要コメントは省略) ...

public class GoalScore_s : MonoBehaviour
{
    // ====== UI ======
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TextMeshProUGUI score_nowText;
    [SerializeField] private TextMeshProUGUI score_totalText;
    [SerializeField] private Slider count_slider;

    // ====== ルール設定 ======
    [Header("カウント個数")]
    [SerializeField] private int countMax = 8;

    // ====== 状態 ======
    private int count = 0;
    private float score_now = 0f;
    private float score_total = 0f; // これが累計スコア

    // ★★★ ランキングシステムから参照するためのゲッター ★★★
    // RankingManager がこの TotalScore (score_total) を読み取れるように public にする
    public float Score { get { return score_total; } }

    // ====== タイマー ======
    private float countTime_up = 0.0f;
    [SerializeField] private float TimeupMAX = 7.0f;

    // ====== 和菓子カウント用Sprite ======
    [SerializeField]
    private GameObject[] countObjects;

    /// <summary>
    /// カウントを受け取って、対応するGameObjectだけを表示する関数
    /// </summary>
    /// <param name="count">現在のカウント数</param>

    // 検索用：タグ名
    private const string TagSweets = "Sweets";
    private const string TagObstacles = "Obstacles";

    // List
    List<GameObject> list = new List<GameObject>();

    //スポナー参照
    [SerializeField] private SpawnManager spawnManager;

    // --- 参照の確定を Awake で ---
    private void Awake()
    {
        if (!count_slider) count_slider = GetComponentInChildren<Slider>(true);
    }

    // --- 初期化 ---
    private void Start()
    {
        UpdateUI();

        if (count_slider)
        {
            count_slider.minValue = 0f;
            count_slider.maxValue = TimeupMAX;
            count_slider.value = 0f;
        }
        else
        {
            Debug.LogWarning("[goal_score] Slider が未割り当てやで", this);
        }
    }

    // --- 毎フレーム処理（タイマー進行／巻き戻し、出荷判定） ---
    private void Update()
    {
        // 必要個数そろっている → カウント進行
        if (count >= countMax)
        {
            countTime_up += Time.deltaTime;
            if (count_slider) count_slider.value = countTime_up;

            // MAX 到達で出荷（今回分を total に加算してリセット）
            if (countTime_up >= TimeupMAX)
            {
                score_total += score_now; // ★スコア(score_total)はここで更新される

                count = 0;
                score_now = 0f;
                UpdateUI(); // UIにも反映

                countTime_up = 0.0f;
                if (count_slider) count_slider.value = 0f;

                // 出荷したから乗ってるアイテムを消すよ
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var go = list[i];
                    if (!go) { list.RemoveAt(i); continue; }

                    if (go.TryGetComponent<JapaneseSweets_Manager>(out var sweet))
                    {
                        spawnManager.DestroySweets(go);
                        sweet.SetReset();
                    }

                    if (go.TryGetComponent<obstacles_Manager>(out var obs))
                        obs.SetReset();
                }
            }
        }
        // そろっていない → カウントを巻き戻す
        else
        {
            if (countTime_up > 0f)
            {
                countTime_up -= Time.deltaTime;
                if (count_slider) count_slider.value = countTime_up;
            }
            else
            {
                if (countTime_up != 0f)
                {
                    countTime_up = 0f;
                    if (count_slider) count_slider.value = 0f;
                }
            }
        }
        UpdateActiveObject();
    }

    // --- 皿に入ってきた（OnTriggerEnter） ---
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(TagSweets) && !other.CompareTag(TagObstacles)) return;
        if (!list.Contains(other.gameObject)) list.Add(other.gameObject);

        // 重量を加算
        JapaneseSweets_Manager sweet;
        obstacles_Manager obstracles_obj;
        if (other.TryGetComponent<JapaneseSweets_Manager>(out sweet))
        {
            count += 1;
            score_now += sweet.GetWeight();
            UpdateUI();
        }
        if (other.TryGetComponent<obstacles_Manager>(out obstracles_obj))
        {
            count += obstracles_obj.GetPeaces();
            score_now -= obstracles_obj.GetWeight();
            UpdateUI();
        }
    }

    // --- 皿から出ていった（OnTriggerExit） ---
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(TagSweets) && !other.CompareTag(TagObstacles)) return;
        list.Remove(other.gameObject); // 抜けたらリストから外す

        // 重量を減算
        JapaneseSweets_Manager sweet;
        obstacles_Manager obstracles_obj;
        if (other.TryGetComponent<JapaneseSweets_Manager>(out sweet))
        {
            count = Mathf.Max(0, count - 1);
            score_now = Mathf.Max(0f, score_now - sweet.GetWeight());
            UpdateUI();
        }
        if (other.TryGetComponent<obstacles_Manager>(out obstracles_obj))
        {
            count = Mathf.Max(0, count - obstracles_obj.GetPeaces());
            score_now = Mathf.Max(0f, score_now - obstracles_obj.GetWeight());
            UpdateUI();
        }
    }

    // --- UI一括更新 ---
    private void UpdateUI()
    {
        if (countText) countText.text = $"Count: {count}";
        if (score_nowText) score_nowText.text = $"Now: {score_now:0.##}";
        if (score_totalText) score_totalText.text = $"Done: {score_total:0.##}";
    }

    public void UpdateActiveObject()
    {
        // 1. まず、配列に入っているGameObjectを「全部非表示」にする
        // ※このやり方だと、配列の数が増えてもコードの変更がいらない
        for (int i = 0; i < countObjects.Length; i++)
        {
            if (countObjects[i] != null)
            {
                // 2. 「i」が「count」と一致した時だけtrue (Active) にする
                //    (例) countが「1」の時
                //    i=0 の時 -> (0 == 1) は false -> SetActive(false)
                //    i=1 の時 -> (1 == 1) は true  -> SetActive(true)
                //    i=2 の時 -> (2 == 1) は false -> SetActive(false)
                bool shouldBeActive = (i < count);
                countObjects[i].SetActive(shouldBeActive);
            }
        }

        // 3. もしカウントが配列の範囲外だった場合の警告（任意）
        if (count < 0 || count >= countObjects.Length)
        {
            Debug.LogWarning("カウント " + count + " に対応するGameObjectがありません。");
            // このロジックだと、範囲外の時は自動的に全部非表示になる
        }
    }
}