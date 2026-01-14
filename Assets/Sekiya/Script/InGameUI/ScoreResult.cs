using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ScoreResult : MonoBehaviour
{
    [System.Serializable]
    public class ResultTeamInfo
    {
        public string teamName;
        public Sprite teamIcon;
        public int teamId;

        // 2人分のマテリアル
        public Material player1Material;
        public Material player2Material;

        [HideInInspector] public float finalScore;
    }

    [Header("チーム設定")]
    [SerializeField] private List<ResultTeamInfo> teamInfos;

    [Header("ランキング表示枠 (UI)")]
    [SerializeField] private List<RankingSlot> rankingSlots;

    [Header("順位ごとのモデル設定 (Prefab 6個)")]
    [SerializeField] private List<GameObject> rankModelPrefabs;

    [Header("キャラ立ち位置 (Transform 6箇所)")]
    [SerializeField] private List<Transform> standPoints;

    void Start()
    {
        ShowResult();
    }

    private void ShowResult()
    {
        List<TeamData> allTeamsData = Ranking.Instance.GetAllTeamsData();

        // スコアを teamInfos に反映
        foreach (var teamData in allTeamsData)
        {
            var targetTeam = teamInfos.Find(t => t.teamId == teamData.teamId);
            if (targetTeam != null)
            {
                targetTeam.finalScore = teamData.score;
            }
        }   



        foreach (var team in teamInfos)
        {
            if (team.teamId == 0) team.finalScore = FinalScore.ScoreTeam0;
            else if (team.teamId == 1) team.finalScore = FinalScore.ScoreTeam1;
            else if (team.teamId == 2) team.finalScore = FinalScore.ScoreTeam2;
        }

        var sortedTeams = teamInfos.OrderByDescending(t => t.finalScore).ToList();

        for (int i = 0; i < rankingSlots.Count; i++)
        {
            if (i < sortedTeams.Count)
            {
                var targetTeam = sortedTeams[i];

                TeamData data = new TeamData();
                data.teamName = targetTeam.teamName;
                data.teamIcon = targetTeam.teamIcon;
                data.score = targetTeam.finalScore;
                rankingSlots[i].SetData(data);

                // --- モデル生成と色変え ---
                int p1Index = i * 2;
                int p2Index = i * 2 + 1;

                // 1人目
                if (CheckIndex(p1Index))
                {
                    SpawnAndColorCharacter(
                        rankModelPrefabs[p1Index],
                        standPoints[p1Index],
                        targetTeam.player1Material,
                        i
                    );
                }

                // 2人目
                if (CheckIndex(p2Index))
                {
                    SpawnAndColorCharacter(
                        rankModelPrefabs[p2Index],
                        standPoints[p2Index],
                        targetTeam.player2Material,
                        i
                    );
                }
            }
            else
            {
                rankingSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private bool CheckIndex(int index)
    {
        return index < rankModelPrefabs.Count && index < standPoints.Count;
    }

    private void SpawnAndColorCharacter(GameObject prefab, Transform point, Material teamMat, int rankIndex)
    {
        if (prefab == null || point == null) return;

        // 生成
        GameObject charObj = Instantiate(prefab, point.position, point.rotation);

        // ▼ 追加：立ち位置（Point）のスケールを、キャラにそのままコピー！
        charObj.transform.localScale = point.localScale;

        // 色変え処理
        if (teamMat != null)
        {
            var renderers = charObj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Material[] mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    mats[m] = teamMat;
                }
                r.materials = mats;
            }
        }

        // アニメーション
        Animator anim = charObj.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetInteger("Rank", rankIndex + 1);
            if (rankIndex == 0) anim.SetTrigger("Win");
        }
    }
}