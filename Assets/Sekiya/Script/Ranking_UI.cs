using UnityEngine;
using System.Collections.Generic; // Listを使うために必要
using System.Linq; // OrderBy (ソート) を使うために必要

public class Ranking_UI : MonoBehaviour
{
    // === インスペクタで設定 ===

    // 1. 3チーム分の元データを設定するリスト
    public List<TeamData> allTeamsData;

    // 2. 順位スロット (UI) を上から順に（1位、2位、3位）設定する
    public List<RankingSlot> rankingSlots;
    // (または配列でもOK)
    // public RankingSlot[] rankingSlots;

    // === 実行例 ===
    void Start()
    {
        // テスト用に、起動時にランキングを更新してみる
        // (実際は、ゲーム終了時やスコア変更時に呼び出す)
        UpdateRanking();
    }

    // (例) テスト用にスコアをランダムに変更する
    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.R)) // Rキーでランキング更新
        //{
        //    // スコアをランダムに更新（テスト用）
        //    foreach (var team in allTeamsData)
        //    {
        //        team.score = Random.Range(0, 100);
        //    }

        //    Debug.Log("ランキングを更新します...");
            UpdateRanking();
        //}
    }


    public void UpdateRanking()
    {
        // ★ 1. GoalScore_s から最新のスコア(float)を取得して更新
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

        // 2. スコアで「降順 (score が高い順)」に並び替え (変更なし)
        List<TeamData> sortedTeams = allTeamsData
                                        .OrderByDescending(team => team.score)
                                        .ToList();

        // 3. UIスロットに、ソートしたデータを上から順に反映 (変更なし)
        for (int i = 0; i < rankingSlots.Count; i++)
        {
            if (i < sortedTeams.Count)
            {
                rankingSlots[i].SetData(sortedTeams[i]);
            }
        }
    }
}