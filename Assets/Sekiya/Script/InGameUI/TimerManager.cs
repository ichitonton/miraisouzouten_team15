using System;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class TimerManager  : NetworkBehaviour
{
    [SerializeField] private NetworkVariable<float> _count = new NetworkVariable<float>(180f);
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private string nextSceneName = "ResultScene";

    private bool isTimeUp = false;

    // ... StartとかOnNetworkSpawnとかはそのまま ...

    void Update()
    {
        // UI更新などはそのまま
        var span = TimeSpan.FromSeconds(_count.Value);
        if (timeText) timeText.text = span.ToString(@"mm\:ss");

        if (!IsServer) return;
        if (isTimeUp) return;

        _count.Value -= Time.deltaTime;

        if (_count.Value <= 0)
        {
            _count.Value = 0;
            isTimeUp = true;
            FinishGame();
        }
    }

    private void FinishGame()
    {
        // 1. まずサーバー側で、現在のスコアを集計する
        float r = 0, b = 0, w = 0;

        // シーン上のゴールを探す
        var goals = FindObjectsByType<GoalToUI>(FindObjectsSortMode.None);
        foreach (var goal in goals)
        {
            if (goal.targetPlayerId == 0) r = goal.Score;
            else if (goal.targetPlayerId == 1) b = goal.Score;
            else if (goal.targetPlayerId == 2) w = goal.Score;
        }

        // 2. 「ClientRpc」を使って、全員（ホスト含む）に数値を強制配布する！
        // これでクライアントのPCにある GameResultData にも数字が入る
        SetScoreClientRpc(r, b, w);

        // 3. ちょっとだけ待つか、そのままシーン遷移
        // （RPCは届くのが速いので、基本はこの順序でOK）
        NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    // ★重要！ 全員のPCで実行される関数
    [ClientRpc]
    private void SetScoreClientRpc(float red, float blue, float white)
    {
        // 受け取った数値を、自分のPCのデータ置き場に保存！
        FinalScore.ScoreTeam0 = red;
        FinalScore.ScoreTeam1 = blue;
        FinalScore.ScoreTeam2 = white;

        Debug.Log($"スコア届いたよ！ R:{red} B:{blue} W:{white}");
    }
}