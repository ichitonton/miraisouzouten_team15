using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro用

public class GameDirector : MonoBehaviour
{
    // ==========================================
    // 1. 和菓子生成（スポーン）の設定
    // ==========================================
    [Header("【第1フェーズ】和菓子生成設定")]
    [Tooltip("生成したいPrefabのリスト（減りません）")]
    [SerializeField] private List<GameObject> wagashiList = new List<GameObject>();
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Tooltip("何秒間生成を続けるか")]
    [SerializeField] private float spawnDuration = 10.0f; // ★ここが新しい終了条件

    [Tooltip("生成間隔（秒）")]
    [SerializeField] private float spawnInterval = 1.0f;

    [Tooltip("開始までの待機時間")]
    [SerializeField] private float startDelay = 1.0f;

    [Tooltip("サイズのランダム幅")]
    [SerializeField] private float minScale = 1.0f;
    [SerializeField] private float maxScale = 1.0f;


    // ==========================================
    // 2. 勝敗判定と表示の設定
    // ==========================================
    [Header("【第2フェーズ】判定と表示設定")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private GameObject resultTextParent;
    [SerializeField] private Color winColor = Color.yellow;
    [SerializeField] private Color loseColor = Color.blue;
    [SerializeField] private float afterResultWaitTime = 2.0f;


    // ==========================================
    // 3. 演出の設定
    // ==========================================
    [Header("【第3フェーズ】演出設定")]
    [SerializeField] private CameraMover cameraMover;
    [SerializeField] private ResultPanelMover uiMover;


    // --------------------------------------------------
    // 処理本体
    // --------------------------------------------------
    private void Start()
    {
        StartCoroutine(GameSequence());
    }

    private IEnumerator GameSequence()
    {
        // 準備
        if (resultTextParent != null) resultTextParent.SetActive(false);

        // --- フェーズ0: 開始待ち ---
        yield return new WaitForSeconds(startDelay);


        // --- フェーズ1: 時間いっぱい和菓子生成 ---
        // 経過時間をカウント
        float timer = 0f;

        while (timer < spawnDuration)
        {
            SpawnWagashi();

            // 指定間隔待つ
            yield return new WaitForSeconds(spawnInterval);

            // 経過時間を足す
            timer += spawnInterval;
        }


        // --- フェーズ2: タイムアップ -> 勝敗判定 & 表示 ---
        Debug.Log("タイムアップ！勝敗を判定します...");
        CheckAndShowResult();

        // 余韻
        yield return new WaitForSeconds(afterResultWaitTime);


        // --- フェーズ3: カメラとUI移動 ---
        Debug.Log("カメラとUI移動開始！");
        if (cameraMover != null) cameraMover.MoveCamera();
        if (uiMover != null) uiMover.MoveIn();
    }

    // ★和菓子生成（リストから削除しないバージョンに戻しました）
    private void SpawnWagashi()
    {
        if (wagashiList.Count == 0 || spawnPoints.Count == 0) return;

        // リストからランダムに選ぶ（削除はしない）
        int wIndex = Random.Range(0, wagashiList.Count);
        int pIndex = Random.Range(0, spawnPoints.Count);

        GameObject prefab = wagashiList[wIndex];
        Transform point = spawnPoints[pIndex];

        // 生成
        GameObject obj = Instantiate(prefab, point.position, point.rotation);

        // サイズ変更
        float scale = Random.Range(minScale, maxScale);
        obj.transform.localScale = Vector3.one * scale;
    }

    // ★勝敗判定ロジック（変更なし）
    private void CheckAndShowResult()
    {
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

        if (resultTextParent != null)
        {
            resultTextParent.SetActive(true);
        }

        Debug.Log($"ID:{myId}(Team{myTeamIndex}) Result:{(isWin ? "WIN" : "LOSE")}");
    }
}