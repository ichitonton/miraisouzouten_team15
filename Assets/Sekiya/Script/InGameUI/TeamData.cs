using UnityEngine;

[System.Serializable]
public class TeamData
{

    public int teamId;          // ★追加（0,1,2…）
    public string teamName;
    public Sprite teamIcon;

    // スコアはRankingManagerが実行時にGoalから取得して設定する
    [HideInInspector] // インスペクタからは非表示でOK
    public float score;
}