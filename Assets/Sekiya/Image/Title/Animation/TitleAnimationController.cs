using UnityEngine;
using System.Collections;

public class TitleAnimationController : MonoBehaviour
{
    [SerializeField] private Animator titleAnimator;

    // 各フェーズの待機時間（秒）を設定できるようにする
    [Header("Phase Settings")]
    [SerializeField] private float phase1Duration = 2.0f; // 1段階目の長さ
    [SerializeField] private float phase2Duration = 1.5f; // 2段階目の長さ
    [SerializeField] private float phase3Duration = 1.0f; // 3段階目の長さ
    // 4段階目は最後なので時間は不要（ループなど）

    void Start()
    {
        // アニメーションシーケンス開始！
        StartCoroutine(PlayTitleSequence());
    }

    private IEnumerator PlayTitleSequence()
    {
        // --- フェーズ 1 開始 ---
        titleAnimator.SetInteger("Phase", 1);
        yield return new WaitForSeconds(phase1Duration); // 指定時間待つ

        // --- フェーズ 2 開始 ---
        titleAnimator.SetInteger("Phase", 2);
        yield return new WaitForSeconds(phase2Duration);

        // --- フェーズ 3 開始 ---
        titleAnimator.SetInteger("Phase", 3);
        yield return new WaitForSeconds(phase3Duration);

        // --- フェーズ 4 (最終) 開始 ---
        titleAnimator.SetInteger("Phase", 4);

        // ここで入力待ちなどを有効にする処理を呼ぶと完璧です
        Debug.Log("タイトルアニメーション完了！入力受付開始");
    }
}