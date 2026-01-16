using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ResultAnimController : MonoBehaviour
{
    private Animator animator;

    // Animationクリップで作ったステート（状態）の名前
    // エディタのAnimatorウィンドウにあるオレンジ色の四角の名前と合わせてください
    [SerializeField] private string animationStateName = "Play";

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// オブジェクトを表示してアニメーションを最初から再生
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        // ステート名, レイヤー(-1), 時間(0=最初から)
        animator.Play(animationStateName, -1, 0f);
    }

    /// <summary>
    /// 非表示にする
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
