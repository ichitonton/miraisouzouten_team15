using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIEventManager : MonoBehaviour
{
    [Header("中央の !")]
    [SerializeField] private RectTransform exclamationRoot;
    [SerializeField] private CanvasGroup exclamationGroup;

    [Header("上から出るテキスト")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private TMP_Text bannerText;

    [Header("Timing")]
    [SerializeField] private float exclamationFadeIn = 0.35f;
    [SerializeField] private float exclamationBlinkDuration = 1.2f;
    [SerializeField] private float exclamationBlinkSpeed = 2.0f; // 大きいほど速い
    [SerializeField] private float exclamationFadeOut = 0.25f;

    [SerializeField] private float bannerSlideIn = 0.35f;
    [SerializeField] private float bannerHold = 1.2f;
    [SerializeField] private float bannerSlideOut = 0.25f;

    [Header("Banner positions (anchored)")]
    [SerializeField] private Vector2 bannerHiddenPos = new Vector2(0, 140); // 画面上に隠す
    [SerializeField] private Vector2 bannerShownPos = new Vector2(0, 40);  // 少しずれた位置

    private Coroutine _running;

    public static UIEventManager Instance {  get; private set; }

    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        ResetUI();
    }

    public void Play(string message)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Sequence(message));
    }

    private IEnumerator Sequence(string message)
    {
        ResetUI();

        // 1) 中央 ! フェードイン
        exclamationRoot.gameObject.SetActive(true);
        yield return Fade(exclamationGroup, 0f, 1f, exclamationFadeIn);

        // 2) ゆっくり点滅（sinで 0.3-1.0 を往復）
        float t = 0f;
        while (t < exclamationBlinkDuration)
        {
            t += Time.unscaledDeltaTime; // UI演出は unscaled 推奨（ポーズでも動く）
            float s = (Mathf.Sin(t * exclamationBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 01
            exclamationGroup.alpha = Mathf.Lerp(0.35f, 1f, s);
            yield return null;
        }

        // 3) フェードアウト
        yield return Fade(exclamationGroup, exclamationGroup.alpha, 0f, exclamationFadeOut);
        exclamationRoot.gameObject.SetActive(false);

        // 4) 上からテキスト出現


        bannerText.text = message;
        bannerRoot.gameObject.SetActive(true);
        bannerRoot.anchoredPosition = bannerHiddenPos;
        bannerGroup.alpha = 0f;

        yield return SlideAndFade(bannerRoot, bannerGroup, bannerHiddenPos, bannerShownPos, 0f, 1f, bannerSlideIn);

        // 5) ホールド
        yield return WaitUnscaled(bannerHold);

        // 6) 戻して消す
        yield return SlideAndFade(bannerRoot, bannerGroup, bannerShownPos, bannerHiddenPos, 1f, 0f, bannerSlideOut);
        bannerRoot.gameObject.SetActive(false);

        _running = null;
    }


    private void ResetUI()
    {
        if (exclamationRoot) exclamationRoot.gameObject.SetActive(false);
        if (bannerRoot) bannerRoot.gameObject.SetActive(false);

        if (exclamationGroup) exclamationGroup.alpha = 0f;
        if (bannerGroup) bannerGroup.alpha = 0f;

        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;
    }

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
    {
        if (dur <= 0f) { g.alpha = to; yield break; }

        float t = 0f;
        g.alpha = from;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            g.alpha = Mathf.Lerp(from, to, a);
            yield return null;
        }
        g.alpha = to;
    }

    private static IEnumerator SlideAndFade(RectTransform rt, CanvasGroup g,
        Vector2 posFrom, Vector2 posTo, float aFrom, float aTo, float dur)
    {
        if (dur <= 0f)
        {
            rt.anchoredPosition = posTo;
            g.alpha = aTo;
            yield break;
        }

        float t = 0f;
        rt.anchoredPosition = posFrom;
        g.alpha = aFrom;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / dur);

            // ちょい気持ちいいイージング（SmoothStep）
            float e = x * x * (3f - 2f * x);

            rt.anchoredPosition = Vector2.Lerp(posFrom, posTo, e);
            g.alpha = Mathf.Lerp(aFrom, aTo, e);
            yield return null;
        }

        rt.anchoredPosition = posTo;
        g.alpha = aTo;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
