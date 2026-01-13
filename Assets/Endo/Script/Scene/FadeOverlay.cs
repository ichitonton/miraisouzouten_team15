using System.Collections;
using System.Diagnostics;
using UnityEngine;

public class FadeOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool dontDestroyOnLoad = true;

    Coroutine _co;

    public static FadeOverlay Instance { get; private set; }

    private void Awake()
    {
        // ★シングルトン確定（多重生成は新しい方を破棄）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        SetAlpha(0f);
        SetBlockRaycasts(false);
    }

    public void FadeOut(float duration) => StartFade(1f, duration, blockRaycasts: true);
    public void FadeIn(float duration)
    {
        UnityEngine.Debug.Log($"[FadeOverlay] FadeIn duration={duration}");
        StartFade(0f, duration, blockRaycasts: false);
    }

    public void SetAlpha(float a)
    {
        if (canvasGroup == null) return;

        // ★ alpha が急に 0 になった瞬間を捕まえる
        if (a <= 0.0001f && canvasGroup.alpha > 0.5f)
        {
            UnityEngine.Debug.LogWarning(
                $"[FadeOverlay] Alpha forced to 0 from {canvasGroup.alpha}\n" +
                new StackTrace(2, true)  // どこから呼ばれたか
            );
        }

        canvasGroup.alpha = a;
    }

    public void SetBlockRaycasts(bool on)
    {
        if (canvasGroup == null) return;
        canvasGroup.blocksRaycasts = on;
        canvasGroup.interactable = on;
    }

    void StartFade(float to, float duration, bool blockRaycasts)
    {
        duration = Mathf.Max(0.05f, duration); // 0.05秒未満は見えにくいので下限
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FadeRoutine(to, duration, blockRaycasts));
    }

    IEnumerator FadeRoutine(float to, float duration, bool blockRaycasts)
    {
        if (canvasGroup == null) yield break;

        float from = canvasGroup.alpha;
        SetBlockRaycasts(true);

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            SetBlockRaycasts(blockRaycasts);
            yield break;
        }

        // ★重要：ロード直後の重いフレームの deltaTime を使わない
        yield return null;

        float start = Time.unscaledTime;

        while (true)
        {
            float t = (Time.unscaledTime - start) / duration;
            if (t >= 1f) break;

            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        SetBlockRaycasts(blockRaycasts);
        _co = null;
    }

    public float GetAlphaDebug() => canvasGroup ? canvasGroup.alpha : -1f;
}
