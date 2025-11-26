using UnityEngine;

public class UICanvasController : MonoBehaviour
{
    // === シーン上のゴールをここに3つ登録するだけでOKにする ===
    [Header("シーン上のゴール")]
    [SerializeField] private GoalScore_Network goalRed;   // 0
    [SerializeField] private GoalScore_Network goalBlue;  // 1
    [SerializeField] private GoalScore_Network goalWhite;// 2

    // === 子要素のスクリプトたち（Inspectorで紐付けてもいいし、自動取得でもOK） ===
    [Header("制御する子要素")]
    [SerializeField] private Exp_Ranking_UI rankingUI;
    [SerializeField] private SceneChangerNetwork sceneChanger;

    // 他にも MyTeamScore とかあればここに追加

    void Start()
    {
        // 1. ランキングにゴール情報を渡す
        if (rankingUI != null)
        {
            rankingUI.SetupGoals(goalRed, goalBlue, goalWhite);
        }

        // 2. シーン遷移ボタンにゴール情報を渡す（スコア保存用）
        if (sceneChanger != null)
        {
            sceneChanger.SetupGoals(goalRed, goalBlue, goalWhite);
        }
    }
}