using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Ranking : MonoBehaviour
{
    // ★内部の TeamData 定義は削除！既存の Exp_TeamData を使う
    [Header("チームデータ（Red, Blue, Yellowの順）")]
    [SerializeField] private List<TeamData> allTeamsData;

    // ★RankingSlot（1位、2位、3位の表示枠）を使う
    [Header("ランキング表示スロット（上から1位, 2位...）")]
    [SerializeField] private List<RankingSlot> rankingSlots;

    // ★追加1：親オブジェクト（Ranking）を入れる場所
    [Header("一括設定用")]
    [SerializeField] private Transform rankingParentRoot;

    // ゴールへの参照リスト（SetupGoalsで受け取る）
    private List<GoalToUI> _goalScripts = new List<GoalToUI>();

    // 親（UICanvasController）からゴールを受け取る関数
    public void SetupGoals(GoalToUI red, GoalToUI blue, GoalToUI white)
    {
        _goalScripts.Clear();
        _goalScripts.Add(red);
        _goalScripts.Add(blue);
        _goalScripts.Add(white);

        red.OnScoreChanged += UpdateRanking;
        blue.OnScoreChanged += UpdateRanking;
        white.OnScoreChanged += UpdateRanking;

        UpdateRanking(); // 初期表示
    }

    //void Update()
    //{
    //    UpdateRanking();
    //}

    public void UpdateRanking()
    {
        // 1. ゴールのスコアを Exp_TeamData に同期する
        for (int i = 0; i < allTeamsData.Count; i++)
        {
            // ゴールがセットされていればスコアを取得
            if (i < _goalScripts.Count && _goalScripts[i] != null)
            {
                allTeamsData[i].score = _goalScripts[i].Score;
            }
        }

        // 2. スコアが高い順に並び替えた「一時的なリスト」を作る
        List<TeamData> sortedTeams = allTeamsData
                                        .OrderByDescending(data => data.score)
                                        .ToList();

        // 3. 順位スロットにデータを流し込む
        for (int i = 0; i < rankingSlots.Count; i++)
        {
            if (i < sortedTeams.Count)
            {
                // スロットにデータを渡して表示更新！
                rankingSlots[i].SetData(sortedTeams[i]);
            }
            else
            {
                // チーム数よりスロットが多い場合はデータなし（null）を渡すなどの処理
                rankingSlots[i].SetData(null);
            }
        }
    }


    //　Rankingをアタッチ後スクリプト名右クリから自動セットアップが可能
    [ContextMenu("Auto Setup Slots")] // ← これを書くとInspectorのメニューに出る！
    private void AutoSetupSlots()
    {
        if (rankingParentRoot == null)
        {
            Debug.LogError("親オブジェクト (RankingParentRoot) をセットしてから実行してね！");
            return;
        }

        // リストを一度クリア
        rankingSlots.Clear();

        // 親の子要素を順番に見ていく（Red -> Blue -> White の順）
        foreach (Transform child in rankingParentRoot)
        {
            // 子要素（UI_RedTeamなど）に RankingSlot がついてたら取得
            RankingSlot slot = child.GetComponent<RankingSlot>();

            // もしついてなければ、その子供（孫）も探してみる
            if (slot == null)
            {
                slot = child.GetComponentInChildren<RankingSlot>();
            }

            // 見つかったらリストに追加
            if (slot != null)
            {
                rankingSlots.Add(slot);
            }
        }

        Debug.Log($"自動設定完了！ {rankingSlots.Count} 個のスロットを登録したよ！");
    }
}