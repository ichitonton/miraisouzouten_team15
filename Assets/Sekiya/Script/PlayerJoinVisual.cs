using UnityEngine;

public class PlayerJoinVisual : MonoBehaviour
{
    [Header("Animation Settings")]
    // ★追加: 最終的な基本サイズ
    [Tooltip("アニメーション完了時のサイズ（例: 0.8）")]
    [SerializeField] private float targetScale = 0.8f;

    [Tooltip("横軸が時間(秒)、縦軸がスケールの「比率」(1.0が最終サイズ)")]
    // カーブのY軸は「最終サイズに対する倍率」として扱います。
    // なので、最後は必ず Y=1.0 で終わるように設定してください。
    [SerializeField]
    private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1.2f), // 勢い余って1.2倍まで膨らむ (実際は 1.2 * 0.8 = 0.96)
        new Keyframe(0.4f, 1.0f)  // 最終的に1.0倍に戻る (実際は 1.0 * 0.8 = 0.8)
    );

    [SerializeField] private float animationSpeed = 1.0f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip joinSound;
    private AudioSource audioSource;

    // アニメーション管理用
    private float currentTime = 0f;
    private bool isAnimating = false;

    private void Awake()
    {
        if (joinSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.clip = joinSound;
        }
        Hide();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        currentTime = 0f;
        isAnimating = true;

        if (audioSource != null && gameObject.activeInHierarchy)
        {
            audioSource.Play();
        }
    }

    public void Hide()
    {
        isAnimating = false;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isAnimating) return;

        currentTime += Time.deltaTime * animationSpeed;

        // カーブから取得した値を「比率」として扱い、目標サイズを掛ける
        float ratio = popCurve.Evaluate(currentTime);
        transform.localScale = Vector3.one * (ratio * targetScale);

        // カーブの最後まで行ったら終了
        if (currentTime >= popCurve.keys[popCurve.length - 1].time)
        {
            // 最後に目標サイズでピタッと確定
            transform.localScale = Vector3.one * targetScale;
            isAnimating = false;
        }
    }
}