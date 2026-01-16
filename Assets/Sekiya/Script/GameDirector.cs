using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using TMPro; // TextMeshProは使わなくなるので削除またはコメントアウト
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameDirector : MonoBehaviour
{
    // ==========================================
    // 1. 和菓子生成（スポーン）の設定
    // ==========================================
    [Header("【第1フェーズ】和菓子生成設定")]
    [SerializeField] private List<GameObject> wagashiList = new List<GameObject>();
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private float spawnDuration = 10.0f;
    [SerializeField] private float spawnInterval = 1.0f;
    [SerializeField] private float startDelay = 1.0f;
    [SerializeField] private float minScale = 1.0f;
    [SerializeField] private float maxScale = 1.0f;

    // ==========================================
    // 2. 勝敗判定と表示の設定
    // ==========================================
    [Header("【第2フェーズ】判定と表示設定")]
    // --- 変更点開始 ---
    // TextMeshPro関連は削除し、画像用GameObjectに変更
    // [SerializeField] private TextMeshProUGUI resultText; // 削除
    [Tooltip("WIN時に表示する画像オブジェクト")]
    [SerializeField] private GameObject winImageObject;   // 追加
    [Tooltip("LOSE時に表示する画像オブジェクト")]
    [SerializeField] private GameObject loseImageObject;  // 追加

    [SerializeField] private GameObject resultTextParent; // ※これは「結果表示全体の親」としてそのまま利用します
    // [SerializeField] private Color winColor = Color.yellow; // 削除
    // [SerializeField] private Color loseColor = Color.blue; // 削除
    // --- 変更点終了 ---

    [SerializeField] private float afterResultWaitTime = 2.0f;

    [SerializeField] Animator player1anim;
    [SerializeField] Animator player2anim;

    // ==========================================
    // 3. 演出の設定
    // ==========================================
    [Header("【第3フェーズ】演出設定")]
    [SerializeField] private CameraMover cameraMover;
    [SerializeField] private ResultPanelMover uiMover;

    // ==========================================
    // 4. シーン遷移とThankYouの設定
    // ==========================================
    [Header("【第4フェーズ】入力と遷移設定")]
    [SerializeField] private GameObject thankYouObject;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool canInput = false;

    // --------------------------------------------------
    // 処理本体
    // --------------------------------------------------
    private void Start()
    {
        StartCoroutine(GameSequence());
    }

    private void Update()
    {
        if (!canInput) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(TransitionSequence(gameSceneName));
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(TransitionSequence(titleSceneName));
        }
    }

    private IEnumerator GameSequence()
    {
        // 初期化
        if (resultTextParent != null) resultTextParent.SetActive(false);
        if (thankYouObject != null) thankYouObject.SetActive(false);

        // --- 変更点開始 ---
        // 個別の画像も念のため非表示にしておく
        if (winImageObject != null) winImageObject.SetActive(false);
        if (loseImageObject != null) loseImageObject.SetActive(false);
        // --- 変更点終了 ---

        // --- フェーズ0: 開始待ち ---
        yield return new WaitForSeconds(startDelay);

        // --- フェーズ1: 時間いっぱい和菓子生成 ---
        float timer = 0f;
        while (timer < spawnDuration)
        {
            SpawnWagashi();
            yield return new WaitForSeconds(spawnInterval);
            timer += spawnInterval;
        }

        // --- フェーズ2: 勝敗判定 ---
        Debug.Log("タイムアップ！勝敗を判定します...");
        CheckAndShowResult();

        // 余韻（Win/Loseが出ている時間）
        yield return new WaitForSeconds(afterResultWaitTime);

        // ★追加変更：カメラが動く前に Win/Lose を消す！
        if (resultTextParent != null)
        {
            resultTextParent.SetActive(false);
        }
        // --- 変更点開始 ---
        // 親を非表示にするので必須ではないですが、安全のため個別画像も非表示に戻す
        if (winImageObject != null) winImageObject.SetActive(false);
        if (loseImageObject != null) loseImageObject.SetActive(false);
        // --- 変更点終了 ---


        // --- フェーズ3: カメラとUI移動 ---
        Debug.Log("カメラとUI移動開始！");
        if (cameraMover != null) cameraMover.MoveCamera();
        if (uiMover != null) uiMover.MoveIn();

        yield return new WaitForSeconds(1.5f);

        // --- フェーズ4: 入力待ち開始 ---
        Debug.Log("入力待機状態になりました");
        canInput = true;
    }

    private IEnumerator TransitionSequence(string nextScene)
    {
        canInput = false;

        if (thankYouObject != null)
        {
            thankYouObject.SetActive(true);
        }

        yield return new WaitForSeconds(3.0f);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(nextScene);
    }

    private void SpawnWagashi()
    {
        if (wagashiList.Count == 0 || spawnPoints.Count == 0) return;
        int wIndex = Random.Range(0, wagashiList.Count);
        int pIndex = Random.Range(0, spawnPoints.Count);
        GameObject prefab = wagashiList[wIndex];
        Transform point = spawnPoints[pIndex];
        GameObject obj = Instantiate(prefab, point.position, point.rotation);
        float scale = Random.Range(minScale, maxScale);
        obj.transform.localScale = Vector3.one * scale;
    }

    private void CheckAndShowResult()
    {
        // ※ FinalScoreクラスの定義が不明なため、ここは元のコードが正しい前提で進めます
        ulong myId = FinalScore.MyPlayerID;
        int s0 = (int)FinalScore.ScoreTeam0;
        int s1 = (int)FinalScore.ScoreTeam1;
        int s2 = (int)FinalScore.ScoreTeam2;

        int myTeamIndex = (int)(myId % 3);

        int myScore = 0;
        if (myTeamIndex == 0) myScore = s0;
        else if (myTeamIndex == 1) myScore = s1;
        else if (myTeamIndex == 2) myScore = s2;

        int maxScore = Mathf.Max(s0, s1, s2);
        bool isWin = (myScore == maxScore);

        // --- 変更点開始 ---
        // テキスト設定処理を削除し、画像の表示切替処理に変更
        /* 以前のコード
        if (resultText != null)
        {
            if (isWin)
            {
                resultText.text = "WIN!!";
                resultText.color = winColor;
            }
            else
            {
                resultText.text = "LOSE...";
                resultText.color = loseColor;
            }
        }
        */

        // 新しいコード：どちらの画像を表示するか選ぶ
        if (winImageObject != null && loseImageObject != null)
        {
            if (isWin)
            {
                // 勝った場合：Win画像を表示、Lose画像を非表示
                winImageObject.SetActive(true);
                loseImageObject.SetActive(false);

                int rand = Random.Range(0, 1);

                if (rand == 0)
                {
                    player1anim.SetTrigger("Win1");
                    player2anim.SetTrigger("Win1");
                }
                else if (rand == 1)
                {
                    player1anim.SetTrigger("Win3L");
                    player2anim.SetTrigger("Win3R");
                }
                else if (rand == 2)
                {
                    player1anim.SetTrigger("Win2L");
                    player2anim.SetTrigger("Win2R");
                }
            }
            else
            {
                // 負けた場合：Win画像を非表示、Lose画像を表示
                winImageObject.SetActive(false);
                loseImageObject.SetActive(true);
                player1anim.SetTrigger("Lose");
                player2anim.SetTrigger("Lose");

            }
        }
        // --- 変更点終了 ---

        // 最後に親オブジェクトを表示して、選択された画像が画面に出るようにする
        if (resultTextParent != null) resultTextParent.SetActive(true);
    }
}