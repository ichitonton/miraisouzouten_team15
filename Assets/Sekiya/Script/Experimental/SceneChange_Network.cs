using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangerNetwork : NetworkBehaviour
{
    [Header("遷移先シーン名")]
    [SerializeField] private string nextSceneName = "ResultScene"; // リザルトシーンの名前

    [Header("スコア集計用（3つのゴールをセットしてね）")]
    private GoalScore_Network goalRed;
    private GoalScore_Network goalBlue;
    private GoalScore_Network goalWhite;

    public void SetupGoals(GoalScore_Network r, GoalScore_Network b, GoalScore_Network w)
    {
        goalRed = r;
        goalBlue = b;
        goalWhite = w;
    }

    // ボタンやタイマーから呼び出す関数
    public void ChangeScene()
    {
        if (!IsServer) return;

        //スコアを静的クラスに保存
        if (goalRed) FinalResultData.ScoreTeam0 = goalRed.Score;
        if (goalBlue) FinalResultData.ScoreTeam1 = goalBlue.Score;
        if (goalWhite) FinalResultData.ScoreTeam2 = goalWhite.Score;

        //シーン遷移
        NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}