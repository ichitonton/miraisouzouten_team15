using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ScoreResult : MonoBehaviour
{
    // インスペクターで設定する「チームの基本情報」
    [System.Serializable]
    public class ResultTeamInfo
    {
        public string teamName;   // "Red" とか
        public Sprite teamIcon;   // アイコン画像
        public int teamId;        // 0=Red, 1=Blue, 2=White
        [HideInInspector] public float finalScore; // ここにスコアを入れる
    }

    [Header("チーム設定 (ID:0=Red, 1=Blue, 2=White)")]
    [SerializeField] private List<ResultTeamInfo> teamInfos;

    [Header("ランキング表示枠 (上から1位, 2位, 3位)")]
    [SerializeField] private List<RankingSlot> rankingSlots;

    void Start()
    {
        ShowResult();
    }

    private void ShowResult()
    {
        // 1. さっき作った「GameResultData」の箱からスコアを取り出す
        foreach (var team in teamInfos)
        {
            if (team.teamId == 0) team.finalScore = FinalScore.ScoreTeam0;
            else if (team.teamId == 1) team.finalScore = FinalScore.ScoreTeam1;
            else if (team.teamId == 2) team.finalScore = FinalScore.ScoreTeam2;
        }

        // 2. スコアが高い順に並び替え（ソート）
        var sortedTeams = teamInfos.OrderByDescending(t => t.finalScore).ToList();

        // 3. UIのスロットに流し込む
        for (int i = 0; i < rankingSlots.Count; i++)
        {
            if (i < sortedTeams.Count)
            {
                // RankingSlotが欲しがっている形（Exp_TeamData）に変換して渡す
                TeamData data = new TeamData();
                data.teamName = sortedTeams[i].teamName;
                data.teamIcon = sortedTeams[i].teamIcon;
                data.score = sortedTeams[i].finalScore;

                rankingSlots[i].SetData(data);
            }
            else
            {
                // データがない枠は隠す
                rankingSlots[i].gameObject.SetActive(false);
            }
        }
    }
}