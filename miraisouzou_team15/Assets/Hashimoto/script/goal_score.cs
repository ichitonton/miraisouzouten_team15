using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;


// 皿（Goal）上のお菓子カウント＆重量・出荷処理＆時間ゲージ表示
// - お菓子が countMax 個そろったらタイマー開始
// - タイマーが MAX に達したら今回の重量を合計に加算してリセット
// - タイマー中に個数が減ったら時間を巻き戻す（0 まで）

public class goal_score : MonoBehaviour
{
	// ====== UI ======
	[Header("UI")]
	[SerializeField] private TextMeshProUGUI countText;       // 皿上の個数表示
	[SerializeField] private TextMeshProUGUI score_nowText;   // 今回バッチの重量表示
	[SerializeField] private TextMeshProUGUI score_totalText; // 累計重量表示
	[SerializeField] private Slider count_slider;             // 時間ゲージ（0→MAX）

	// ====== ルール設定 ======
	[Header("カウント個数")]
	[SerializeField] private int countMax = 3;                // 出荷に必要な個数

	// ====== 状態 ======
	private int count = 0;									// 今、皿に乗っている個数
	private float score_now = 0f;							// 今回バッチ（出荷候補）の合計重量
	private float score_total = 0f;							// これまでの出荷済み合計重量

	// ====== タイマー ======
	private float countTime_up = 0.0f;                        // そろってからの経過時間
	[SerializeField] private float TimeupMAX = 7.0f;          // 出荷に必要な時間（秒）

	// 検索用：タグ名
	private const string TagSweets = "Sweets";

	// List
	List<GameObject> list = new List<GameObject>();

	// --- 参照の確定を Awake で（Inspector 優先 / 無ければ子から拾う） ---
	private void Awake()
	{
		if (!count_slider) count_slider = GetComponentInChildren<Slider>(true);
	}

	// --- 初期化（UI＆スライダー設定） ---
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
			if (count_slider) count_slider.value = countTime_up; // nullガード

			// MAX 到達で出荷（今回分を total に加算してリセット）
			if (countTime_up >= TimeupMAX)
			{
				score_total += score_now;

				count = 0;
				score_now = 0f;
				UpdateUI();

				countTime_up = 0.0f;
				if (count_slider) count_slider.value = 0f;

				// 出荷したから乗ってるアイテムを消すよ
				foreach (GameObject value in list)
				{ 
					Destroy(value, 1);
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
	}

	// --- 皿に入ってきた（OnTriggerEnter） ---
	private void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag(TagSweets)) return;

		list.Add(other.gameObject);

		// 重量を加算
		JapaneseSweets_Manager sweet;
		if (other.TryGetComponent<JapaneseSweets_Manager>(out sweet))
		{
			count += 1;
			score_now += sweet.GetWeight();
			UpdateUI();
		}
	}

	// --- 皿から出ていった（OnTriggerExit） ---
	private void OnTriggerExit(Collider other)
	{
		if (!other.CompareTag(TagSweets)) return;

		// 重量を減算
		JapaneseSweets_Manager sweet;
		if (other.TryGetComponent<JapaneseSweets_Manager>(out sweet))
		{
			count = Mathf.Max(0, count - 1);
			score_now = Mathf.Max(0f, score_now - sweet.GetWeight());
			UpdateUI();
		}
	}

	// --- UI一括更新 ---
	private void UpdateUI()
	{
		if (countText) countText.text = $"Count: {count}";
		if (score_nowText) score_nowText.text = $"Now: {score_now:0.##}";
		if (score_totalText) score_totalText.text = $"Total: {score_total:0.##}";
	}
}
