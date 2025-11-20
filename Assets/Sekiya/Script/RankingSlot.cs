using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProを使う場合

public class RankingSlot : MonoBehaviour
{
    // インスペクタで、このスロットが管理するUIコンポーネントを設定
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI scoreText;

    // TeamDataを受け取って、自分のUIを更新するメソッド
    public void SetData(TeamData data)
    {
        if (data == null) return; // データがなければ何もしない

        // データをUIに反映
        if (iconImage != null)
        {
            iconImage.sprite = data.teamIcon;
        }

        // (おまけ) スコアも表示する場合
        if (scoreText != null)
        {
            scoreText.text = data.score.ToString();
        }
    }
}