using UnityEngine;

public class ChangePlayMode : MonoBehaviour
{
    // ここにヒエラルキーにある画像オブジェクトをセットしてね
    [Header("UI Images")]
    public GameObject hostImage;   // ホスト用の画像（例: "HOST"の文字など）
    public GameObject clientImage; // クライアント用の画像（例: "CLIENT"の文字など）

    // 今ホストモードかどうかを管理するフラグ（最初はtrue=ホストにしておく）
    private bool isHostMode = true;

    void Start()
    {
        // ゲーム開始時に一度UIの状態を反映させる
        UpdateUI();
    }

    void Update()
    {
        // スペースキーが押されたら
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // モードを反転させる（trueならfalseに、falseならtrueに）
            isHostMode = !isHostMode;

            // 画面の表示を更新
            UpdateUI();

            // 確認用ログ
            Debug.Log("現在のモード: " + (isHostMode ? "HOST" : "CLIENT"));
        }

        // （おまけ）Enterキーで決定した時の処理例
        if (Input.GetKeyDown(KeyCode.Return))
        {
            StartGame();
        }
    }

    // 画像の表示・非表示を切り替えるメソッド
    void UpdateUI()
    {
        // ホストモードなら hostImage を表示、clientImage を非表示
        if (isHostMode)
        {
            hostImage.SetActive(true);
            clientImage.SetActive(false);
        }
        else // クライアントモードなら逆
        {
            hostImage.SetActive(false);
            clientImage.SetActive(true);
        }
    }

    // ゲーム開始処理（仮）
    void StartGame()
    {
        if (isHostMode)
        {
            Debug.Log("ホストとしてゲーム開始！");
            // ここに NetworkManager.Singleton.StartHost(); などを書く
        }
        else
        {
            Debug.Log("クライアントとしてゲーム開始！");
            // ここに NetworkManager.Singleton.StartClient(); などを書く
        }
    }
}