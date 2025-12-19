using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameCountdown : MonoBehaviour
{
    [Header("表示するUIのImageコンポーネント")]
    [SerializeField] private Image countdownDisplay;

    [Header("カウントダウン画像のリスト（表示順）")]
    [SerializeField] private Sprite[] countdownSprites;

    [Header("Countdown / 3 (float)")]
    [SerializeField] private float Countdown;



    void Start()
    {
        // ゲーム開始時にカウントダウンを開始
        StartCoroutine(CountdownSequence());
    }

    // カウントダウンの処理
    IEnumerator CountdownSequence()
    {
        // 画像を順番に表示するループ
        for (int i = 0; i < countdownSprites.Length; i++)
        {
            // 画像をセット
            countdownDisplay.sprite = countdownSprites[i];

            // Countdownを3で割った時間だけ待機
            yield return new WaitForSeconds(Countdown / 3.0f);
        }

        // カウントダウンが終わったら画像を非表示にする
        countdownDisplay.gameObject.SetActive(false);

        // ゲーム本編を開始する処理を呼ぶ
        StartGame();
    }

    void StartGame()
    {
        Debug.Log("ゲームスタート！");
        // ここにプレイヤーを動けるようにしたり、敵をスポーンさせる処理を書く
        // 例: playerController.enabled = true;
    }
}