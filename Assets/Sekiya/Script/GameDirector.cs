using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    [Tooltip("WIN時に表示する画像オブジェクト")]
    [SerializeField] private GameObject winImageObject;
    [Tooltip("LOSE時に表示する画像オブジェクト")]
    [SerializeField] private GameObject loseImageObject;

    [Tooltip("WIN時に再生するエフェクトオブジェクト")]
    [SerializeField] private GameObject winEffectObject;

    [SerializeField] private GameObject resultTextParent;

    [SerializeField] private float afterResultWaitTime = 2.0f;

    [SerializeField] Animator player1anim;
    [SerializeField] Animator player2anim;

    // ==========================================
    // 3. 演出の設定
    // ==========================================
    [Header("【第3フェーズ】演出設定")]
    [SerializeField] private CameraMover cameraMover;
    [SerializeField] private CameraMover EffectMover;
    [SerializeField] private ResultPanelMover uiMover;

    // ==========================================
    // 4. シーン遷移とThankYouの設定
    // ==========================================
    [Header("【第4フェーズ】入力と遷移設定")]
    [SerializeField] private GameObject thankYouObject;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool canInput = false;

    [SerializeField] ResultSetSkinMaterial _resultSetSkinMaterial1;
    [SerializeField] ResultSetSkinMaterial _resultSetSkinMaterial2;


    [SerializeField] private float thankTime = 3f;
    // --------------------------------------------------
    // 処理本体
    // --------------------------------------------------
    private void Start()
    {
        StartCoroutine(GameSequence());

        VideoFadeManager.Instance.fadeInDuration = 1.0f;
        VideoFadeManager.Instance.fadeOutDuration = 1.0f;
    }

    private void Update()
    {
        if (!canInput) return;

        if (Input.GetKeyDown(KeyCode.R))
        {

            StartCoroutine(ActiveThankyou(gameSceneName));
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(ActiveThankyou(titleSceneName));

        }
        if (Gamepad.all.Count >= 0)
        {
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.bButton.wasPressedThisFrame)
                {

                    StartCoroutine(ActiveThankyou(gameSceneName));
                }
                if (gamepad.aButton.wasPressedThisFrame)
                {

                    StartCoroutine(ActiveThankyou(titleSceneName));
                }
            }
        }
    }

    private IEnumerator GameSequence()
    {
        // 初期化
        if (resultTextParent != null) resultTextParent.SetActive(false);
        if (thankYouObject != null) thankYouObject.SetActive(false);

        // 画像とエフェクトを初期化（非表示）
        if (winImageObject != null) winImageObject.SetActive(false);
        if (loseImageObject != null) loseImageObject.SetActive(false);
        if (winEffectObject != null) winEffectObject.SetActive(false);

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

        // ★変更点：ここで非表示にする処理を削除しました。
        // これにより、カメラが動いても画像やエフェクトは出たままになります。

        // --- フェーズ3: カメラとUI移動 ---
        Debug.Log("カメラとUI移動開始！");
        if (cameraMover != null) cameraMover.MoveCamera();
        if (EffectMover != null)
        {
            if (EffectMover.gameObject.activeSelf == true)
            {
                if (EffectMover.enabled == true) EffectMover.MoveCamera();
            }
        }
            

        if (uiMover != null) uiMover.MoveIn();

        yield return new WaitForSeconds(1.5f);

        // --- フェーズ4: 入力待ち開始 ---
        Debug.Log("入力待機状態になりました");
        canInput = true;
    }

    private IEnumerator TransitionSequence(string nextScene)
    {
        canInput = false;

        yield return new WaitForSeconds(thankTime);

        VideoFadeManager.Instance.PlayToScene(nextScene, VideoFadeManager.FadeScope.LocalOnly);
        
    }

    private IEnumerator ActiveThankyou(string nextScene)
    {
        //ネットを落とす
        NetworkShutdownRelay.Instance.ShutDown();

        var video = VideoFadeManager.Instance;

        video.PlayFadeOnly(VideoFadeManager.FadeScope.LocalOnly);

        yield return new WaitForSeconds(video.fadeOutDuration);

        if (thankYouObject != null)
        {
            thankYouObject.SetActive(true);
        }

        StartCoroutine(TransitionSequence(nextScene));

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

        if (winImageObject != null && loseImageObject != null)
        {
            if (isWin)
            {
                // 勝った場合
                winImageObject.SetActive(true);
                loseImageObject.SetActive(false);

                // エフェクト表示
                if (winEffectObject != null) winEffectObject.SetActive(true);

                _resultSetSkinMaterial1.SetSkinMaterialWin();
                _resultSetSkinMaterial2.SetSkinMaterialWin();

                // アニメーション分岐 (Random.Rangeはintの場合、最大値を含まないので3にする)
                int rand = Random.Range(0, 2);

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
                //else if (rand == 2)
                //{
                //    player1anim.SetTrigger("Win2L");
                //    player2anim.SetTrigger("Win2R");
                //}
            }
            else
            {
                _resultSetSkinMaterial1.SetSkinMaterialLose();
                _resultSetSkinMaterial2.SetSkinMaterialLose();
                // 負けた場合
                winImageObject.SetActive(false);
                loseImageObject.SetActive(true);

                // エフェクト非表示
                if (winEffectObject != null) winEffectObject.SetActive(false);

                player1anim.SetTrigger("Lose");
                player2anim.SetTrigger("Lose");
            }
        }

        // 親オブジェクト表示
        if (resultTextParent != null) resultTextParent.SetActive(true);
    }
}