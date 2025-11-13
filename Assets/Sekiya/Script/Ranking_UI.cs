using UnityEngine;
using System.Collections.Generic; // Listを使うために必要
using System.Linq; // OrderBy (ソート) を使うために必要

public class Ranking_UI : MonoBehaviour
{

    //3チーム分の元データを設定するリスト
    public List<TeamData> allTeamsData;

    //順位スロット (UI) を上から順に（1位、2位、3位）設定する
    public List<RankingSlot> rankingSlots;

    // === 実行例 ===
    void Start()
    {
         UpdateRanking();
    }

    void Update()
    {
        UpdateRanking();
    }


    public void UpdateRanking()
    {
        //GoalScore_s から最新のスコア(float)を取得して更新
        foreach (var team in allTeamsData)
        {
            if (team.teamGoalScript != null)
            {
                // GoalScore_s の "Score" プロパティ (float) から値を取得
                team.score = team.teamGoalScript.Score;
            }
            else
            {
                Debug.LogWarning(team.teamName + " にGoalスクリプトが設定されていません。");
            }
        }

        //スコアで「降順 (score が高い順)」に並び替え (変更なし)
        List<TeamData> sortedTeams = allTeamsData
                                        .OrderByDescending(team => team.score)
                                        .ToList();

        //UIスロットに、ソートしたデータを上から順に反映 (変更なし)
        for (int i = 0; i < rankingSlots.Count; i++)
        {
            if (i < sortedTeams.Count)
            {
                rankingSlots[i].SetData(sortedTeams[i]);
            }
        }
    }
}