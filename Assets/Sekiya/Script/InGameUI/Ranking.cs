using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Ranking : NetworkBehaviour
{
    public static Ranking Instance { get; private set; }

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

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
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
    public int GetRankByTeamId(int teamId)
    {
        // スコア降順
        var sorted = allTeamsData
            .OrderByDescending(t => t.score)
            .ToList();

        // 該当チーム
        TeamData me = sorted.FirstOrDefault(t => t.teamId == teamId);
        if (me == null)
        {
            Debug.LogError($"TeamId {teamId} が Ranking に存在しない");
            return -1;
        }

        float topScore = sorted[0].score;

        // 1位が複数いるか？
        bool isTopTie = sorted.Count(t => t.score == topScore) > 1;

        // 自分が1位グループ
        if (me.score == topScore)
        {
            return isTopTie ? 3 : 1; // 同率1位は全員3位扱い
        }

        // 2位候補のスコア
        float secondScore = sorted
            .Where(t => t.score < topScore)
            .Select(t => t.score)
            .FirstOrDefault();

        // 2位が存在しない（全員同点など）
        if (secondScore == 0 && sorted.All(t => t.score == topScore))
        {
            return 3;
        }

        bool isSecondTie = sorted.Count(t => t.score == secondScore) > 1;

        if (me.score == secondScore)
        {
            return isSecondTie ? 3 : 2;
        }

        // それ以外は最下位
        return 3;
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



