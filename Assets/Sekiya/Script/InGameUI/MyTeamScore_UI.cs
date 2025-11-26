using UnityEngine;
using TMPro;
using UnityEngine.UI;

// UIオブジェクト（Canvas内の親）に貼り付ける
public class MyTeamScore_UI : MonoBehaviour
{
    // どこからでも呼べるようにする（シングルトン的扱い）
    public static MyTeamScore_UI Instance;

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI totalText;
    public Slider timerSlider;

    private void Awake()
    {
        Instance = this; // 自分自身を登録
    }

    // ゴール側から数値を送ってもらう関数
    public void UpdateDisplay(float now, float total, float time)
    {
        if (scoreText) scoreText.text = $"Now: {now:0.##}";
        if (totalText) totalText.text = $"Done: {total:0.##}";
        if (timerSlider) timerSlider.value = time;
    }

    // スライダー設定用
    public void InitSlider(float maxTime)
    {
        if (timerSlider)
        {
            timerSlider.maxValue = maxTime;
            timerSlider.value = 0;
        }
    }
}