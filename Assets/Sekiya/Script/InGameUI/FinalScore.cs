// 静的（static）クラスにして、どこからでもアクセスできるようにする
public static class FinalScore
{
    // 3チーム分のスコアを保存しておく変数
    public static float ScoreTeam0; // 赤？
    public static float ScoreTeam1; // 青？
    public static float ScoreTeam2; // 白？

    // データをリセットする便利関数
    public static void Reset()
    {
        ScoreTeam0 = 0;
        ScoreTeam1 = 0;
        ScoreTeam2 = 0;
    }
}