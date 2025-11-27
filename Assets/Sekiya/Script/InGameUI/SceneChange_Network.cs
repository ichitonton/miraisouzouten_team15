using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangerNetwork : NetworkBehaviour
{
    [Header("遷移先シーン名")]
    [SerializeField] private string nextSceneName = "ResultScene"; // リザルトシーンの名前

    [Header("スコア集計用（3つのゴールをセットしてね）")]
    private GoalToUI goalRed;
    private GoalToUI goalBlue;
    private GoalToUI goalWhite;

    public void SetupGoals(GoalToUI r, GoalToUI b, GoalToUI w)
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
        if (goalRed) FinalScore.ScoreTeam0 = goalRed.Score;
        if (goalBlue) FinalScore.ScoreTeam1 = goalBlue.Score;
        if (goalWhite) FinalScore.ScoreTeam2 = goalWhite.Score;

        //シーン遷移
        NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}