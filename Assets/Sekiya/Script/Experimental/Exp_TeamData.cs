using UnityEngine;

[System.Serializable]
public class Exp_TeamData
{
    public string teamName;
    public Sprite teamIcon;

    // スコアはRankingManagerが実行時にGoalから取得して設定する
    [HideInInspector] // インスペクタからは非表示でOK
    public float score;
}