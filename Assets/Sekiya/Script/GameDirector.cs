using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    // ★変更: GameObjectではなく、アニメ制御スクリプトを参照
    [Tooltip("WIN画像についているResultAnimController")]
    [SerializeField] private ResultAnimController winAnimController;

    [Tooltip("LOSE画像についているResultAnimController")]
    [SerializeField] private ResultAnimController loseAnimController;

    [SerializeField] private GameObject resultTextParent; // 結果表示全体の親（もしあれば）
    [SerializeField] private float afterResultWaitTime = 2.0f;

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
        // --- 初期化: 全てのUIを隠す ---
        if (resultTextParent != null) resultTextParent.SetActive(false);
        if (thankYouObject != null) thankYouObject.SetActive(false);

        // ★アニメコントローラーを使って非表示にする
        if (winAnimController != null) winAnimController.Hide();
        if (loseAnimController != null) loseAnimController.Hide();

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
        CheckAndShowResult(); // WIN/LOSEのアニメーション再生開始

        // 余韻（Win/Loseが出ている状態で待機）
        yield return new WaitForSeconds(afterResultWaitTime);

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
        // スコア取得ロジック（既存のまま）
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

        // ★アニメーション再生処理
        if (isWin)
        {
            if (winAnimController != null) winAnimController.Show();
            if (loseAnimController != null) loseAnimController.Hide();
        }
        else
        {
            if (winAnimController != null) winAnimController.Hide();
            if (loseAnimController != null) loseAnimController.Show();
        }

        // 最後に親オブジェクトを表示（もし使っていれば）
        if (resultTextParent != null) resultTextParent.SetActive(true);
    }
}