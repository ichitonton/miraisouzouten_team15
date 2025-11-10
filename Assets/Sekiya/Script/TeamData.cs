using UnityEngine;

[System.Serializable]
public class TeamData
{
    public string teamName;
    public Sprite teamIcon;

    // ★追加： このチームのスコアを取得するGoalスクリプト
    public GoalScore_s teamGoalScript;

    // スコアはRankingManagerが実行時にGoalから取得して設定する
    [HideInInspector] // インスペクタからは非表示でOK
    public float score;
}