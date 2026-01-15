using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangerNetwork : NetworkBehaviour
{
    [Header("遷移先シーン名")]
    [SerializeField] private string nextSceneName = "Beta_Result";

    // スコア集計用
    private GoalToUI goalRed;
    private GoalToUI goalBlue;
    private GoalToUI goalWhite;

    public void SetupGoals(GoalToUI r, GoalToUI b, GoalToUI w)
    {
        goalRed = r;
        goalBlue = b;
        goalWhite = w;
    }

    // ボタンやタイマーから呼び出す関数（サーバーのみ実行可能）
    public void ChangeScene()
    {
        if (!IsServer) return;

        // 1. 現在のスコアを取得
        int s0 = goalRed ? (int)goalRed.Score : 0;
        int s1 = goalBlue ? (int)goalBlue.Score : 0;
        int s2 = goalWhite ? (int)goalWhite.Score : 0;

        // 2. 全員（クライアント含む）に向けて「スコア保存命令」を出す
        // ※この命令はネットを通して全プレイヤーのPCで実行されます
        SaveDataAndSceneChangeClientRpc(s0, s1, s2);

        // 3. シーン遷移を実行
        FadeManager.Instance.PlayToScene(nextSceneName, FadeManager.FadeScope.LocalOnly);
    }

    // ▼▼ ここが重要：全員のPCで実行される処理 ▼▼
    [ClientRpc]
    private void SaveDataAndSceneChangeClientRpc(int score0, int score1, int score2)
    {
        Debug.Log("リザルト遷移準備：スコアとIDを保存します");

        // --- A. スコアの保存 ---
        FinalScore.ScoreTeam0 = score0;
        FinalScore.ScoreTeam1 = score1;
        FinalScore.ScoreTeam2 = score2;

        // --- B. 自分のIDの保存 ---
        // このコードは各プレイヤーのPCで動いているので、
        // LocalClientId を呼べば「自分自身のID」が取れます。
        FinalScore.MyPlayerID = NetworkManager.Singleton.LocalClientId;

        Debug.Log($"保存完了！ ID:{FinalScore.MyPlayerID} / Scores: {score0}, {score1}, {score2}");
    }
}