using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UIイベント演出マネージャー
/// - 中央の「!」＋（任意で）中央ワーニングアイコンを表示
/// - その点滅（Sin/Cos）に同期して、画面全体のフラッシュも点滅させる（任意）
/// - 最後に上からメッセージバナーをスライド表示
/// - ★同時イベント対応：ワーニングアイコンを2つ表示（左右にずらす）＋メッセージ結合
///
/// 設計方針（バグ予防）
/// - “表示/非表示”は基本 CanvasGroup.alpha と Image.enabled / SetActive で制御
/// - ResetUI() では StopFlash() を呼ばない（Sequence開始時にだけ StopFlash）
/// - Play() は常に1本だけ（コルーチン重複を止める）
/// </summary>
public class UIEventManager : MonoBehaviour
{
    // ================================
    // Inspector references
    // ================================

    [Header("中央の ! (Existing)")]
    [SerializeField] private RectTransform exclamationRoot;
    [SerializeField] private CanvasGroup exclamationGroup;

    [Header("Center Warning Icon (optional)")]
    [SerializeField] private Image warningIconImage;
    [SerializeField] private CanvasGroup warningIconGroup;

    [Header("Center Warning Icon #2 (optional / Double)")]
    [SerializeField] private Image warningIconImage2;
    [SerializeField] private CanvasGroup warningIconGroup2;
    [SerializeField, Min(0f)] private float warningOffsetX = 60f;

    [Header("上から出るテキスト")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private TMP_Text bannerText;

    [Header("Screen Flash Overlay (optional)")]
    [SerializeField] private Image flashOverlayImage;
    [SerializeField] private CanvasGroup flashOverlayGroup;

    [Header("WarningSide (optional)")]
    [SerializeField] private GameObject _warningSide = null;
    [SerializeField] private CanvasGroup warningSideGroup = null; // ★CanvasGroupを入れる
    [SerializeField, Min(0f)] private float warningSideFadeIn = 0.20f;
    [SerializeField, Min(0f)] private float warningSideFadeOut = 0.20f;

    // ================================
    // Timing settings
    // ================================

    [Header("Timing")]
    [SerializeField] private float exclamationFadeIn = 0.35f;
    [SerializeField] private float exclamationBlinkDuration = 1.2f;
    [SerializeField] private float exclamationBlinkSpeed = 2.0f;
    [SerializeField] private float exclamationFadeOut = 0.25f;

    [SerializeField] private float bannerSlideIn = 0.35f;
    [SerializeField] private float bannerHold = 1.2f;
    [SerializeField] private float bannerSlideOut = 0.25f;

    [Header("Banner positions (anchored)")]
    [SerializeField] private Vector2 bannerHiddenPos = new Vector2(0, 140);
    [SerializeField] private Vector2 bannerShownPos = new Vector2(0, 40);

    // ================================
    // Runtime state
    // ================================

    private ScreenFlashSetting _activeFlash;
    private bool _flashActive;

    private Coroutine _running;
    private Coroutine _warningSideFadeRoutine;

    public static UIEventManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (warningSideGroup == null && _warningSide != null)
            warningSideGroup = _warningSide.GetComponent<CanvasGroup>();

        ResetUI();
        StopFlash();
    }

    // -------------------------------
    // Public API
    // -------------------------------

    public void Play(string message) => Play(message, null, null);

    public void Play(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SequenceSingle(message, warningIcon, flash));
    }

    public void PlayDouble(string messageCombined, Sprite iconA, Sprite iconB, ScreenFlashSetting flashCombined)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SequenceDouble(messageCombined, iconA, iconB, flashCombined));
    }

    // -------------------------------
    // Main sequences
    // -------------------------------

    private IEnumerator SequenceSingle(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        ResetUI();
        StopFlash();

        // 表示対象確定（初フレームポップ予防）
        SetupWarningIconSingle(warningIcon);
        SetupWarningIconDouble(null, null); // ★2個目側だけ確実に消す（warningSideは触らない）
        SetupFlash(flash);

        // ★warningSide：アイコンがある時だけ出す（必要ならここをtrue固定でもOK）
        bool wantWarningSide = (warningIcon != null);
        StartWarningSide(wantWarningSide);

        SetExclamationVisible(true);

        yield return StartCoroutine(BlinkCoreRoutine());

        yield return StartCoroutine(BannerRoutine(message));

        // ★演出が終わると同時にwarningSideをフェードアウトしてOFF
        yield return StopWarningSide();

        _running = null;
    }

    private IEnumerator SequenceDouble(string messageCombined, Sprite iconA, Sprite iconB, ScreenFlashSetting flashCombined)
    {
        ResetUI();
        StopFlash();

        SetupWarningIconSingle(null);
        SetupWarningIconDouble(iconA, iconB);
        SetupFlash(flashCombined);

        bool wantWarningSide = (iconA != null || iconB != null);
        StartWarningSide(wantWarningSide);

        SetExclamationVisible(true);

        yield return StartCoroutine(BlinkCoreRoutine());

        yield return StartCoroutine(BannerRoutine(messageCombined));

        yield return StopWarningSide();

        _running = null;
    }

    // -------------------------------
    // Core routines
    // -------------------------------

    private IEnumerator BlinkCoreRoutine()
    {
        float t = 0f;
        float w = exclamationBlinkSpeed * Mathf.PI * 2f;

        // 点滅（cosで0スタート）
        while (t < exclamationBlinkDuration)
        {
            float s = (1f - Mathf.Cos(t * w)) * 0.5f; // 0..1
            t += Time.unscaledDeltaTime;

            float warnA = Mathf.Lerp(0.35f, 1f, s);

            if (exclamationGroup != null) exclamationGroup.alpha = warnA;

            ApplyIconAlpha(warningIconImage, warningIconGroup, warnA);
            ApplyIconAlpha(warningIconImage2, warningIconGroup2, warnA);

            if (_flashActive && _activeFlash != null)
            {
                float flashA = Mathf.Lerp(0f, Mathf.Clamp01(_activeFlash.maxAlpha), s);
                SetFlashAlpha(flashA);
            }

            yield return null;
        }

        // フェードアウト（!）
        if (exclamationGroup != null)
            yield return Fade(exclamationGroup, exclamationGroup.alpha, 0f, exclamationFadeOut);

        // アイコンは表示しているものだけ落とす
        if (IsIconActive(warningIconImage))
            yield return FadeIconTo(warningIconImage, warningIconGroup, 0f, exclamationFadeOut);
        if (IsIconActive(warningIconImage2))
            yield return FadeIconTo(warningIconImage2, warningIconGroup2, 0f, exclamationFadeOut);

        StopFlash();

        SetExclamationVisible(false);
        SetIconVisible(warningIconImage, warningIconGroup, false);
        SetIconVisible(warningIconImage2, warningIconGroup2, false);
    }

    private IEnumerator BannerRoutine(string message)
    {
        if (bannerText) bannerText.text = message ?? "";

        if (bannerRoot) bannerRoot.gameObject.SetActive(true);
        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;
        if (bannerGroup) bannerGroup.alpha = 0f;

        yield return SlideAndFade(bannerRoot, bannerGroup, bannerHiddenPos, bannerShownPos, 0f, 1f, bannerSlideIn);
        yield return WaitUnscaled(bannerHold);
        yield return SlideAndFade(bannerRoot, bannerGroup, bannerShownPos, bannerHiddenPos, 1f, 0f, bannerSlideOut);

        if (bannerRoot) bannerRoot.gameObject.SetActive(false);
    }

    // -------------------------------
    // Setup helpers
    // -------------------------------

    private void SetupWarningIconSingle(Sprite icon)
    {
        if (warningIconImage == null) return;

        if (icon == null)
        {
            SetIconVisible(warningIconImage, warningIconGroup, false);
            return;
        }

        warningIconImage.sprite = icon;
        SetIconVisible(warningIconImage, warningIconGroup, true);
        ApplyIconAlpha(warningIconImage, warningIconGroup, 0f);
    }

    private void SetupWarningIconDouble(Sprite iconA, Sprite iconB)
    {
        // 2個目未設定なら保険：Aだけ単発側に出す
        if (warningIconImage2 == null)
        {
            SetupWarningIconSingle(iconA);
            return;
        }

        SetupIconAt(warningIconImage, warningIconGroup, iconA, -warningOffsetX);
        SetupIconAt(warningIconImage2, warningIconGroup2, iconB, +warningOffsetX);
    }

    private void SetupIconAt(Image img, CanvasGroup cg, Sprite icon, float offsetX)
    {
        if (img == null) return;

        if (icon == null)
        {
            SetIconVisible(img, cg, false);
            return;
        }

        img.sprite = icon;
        SetIconVisible(img, cg, true);

        if (img.rectTransform != null)
        {
            var p = img.rectTransform.anchoredPosition;
            p.x = offsetX;
            img.rectTransform.anchoredPosition = p;
        }

        ApplyIconAlpha(img, cg, 0f);
    }

    private void SetupFlash(ScreenFlashSetting s)
    {
        _activeFlash = s;
        _flashActive = (s != null && flashOverlayImage != null);

        if (!_flashActive) return;

        flashOverlayImage.enabled = true;

        if (s.overlaySprite != null)
        {
            flashOverlayImage.sprite = s.overlaySprite;
            flashOverlayImage.color = Color.white;
        }
        else
        {
            flashOverlayImage.sprite = null;
            flashOverlayImage.color = s.flashColor;
        }

        SetFlashAlpha(0f);
    }

    // -------------------------------
    // WarningSide control (fade-in -> keep -> fade-out)
    // -------------------------------

    private void StartWarningSide(bool on)
    {
        if (_warningSide == null || warningSideGroup == null)
        {
            // CanvasGroup無しなら従来通り即ON/OFF（任意）
            if (_warningSide != null) _warningSide.SetActive(on);
            return;
        }

        // フェード中なら止める
        if (_warningSideFadeRoutine != null) StopCoroutine(_warningSideFadeRoutine);
        _warningSideFadeRoutine = null;

        if (!on)
        {
            _warningSide.SetActive(false);
            warningSideGroup.alpha = 0f;
            return;
        }

        // ON：透明からフェードイン
        _warningSide.SetActive(true);
        warningSideGroup.alpha = 0f;
        _warningSideFadeRoutine = StartCoroutine(Fade(warningSideGroup, 0f, 1f, warningSideFadeIn));
    }

    private IEnumerator StopWarningSide()
    {
        if (_warningSide == null) yield break;

        if (warningSideGroup == null)
        {
            _warningSide.SetActive(false);
            yield break;
        }

        // フェード中なら止めてからフェードアウトへ
        if (_warningSideFadeRoutine != null) StopCoroutine(_warningSideFadeRoutine);
        _warningSideFadeRoutine = null;

        if (!_warningSide.activeSelf) yield break;

        yield return Fade(warningSideGroup, warningSideGroup.alpha, 0f, warningSideFadeOut);
        _warningSide.SetActive(false);
    }

    // -------------------------------
    // Apply / Stop helpers
    // -------------------------------

    private void StopFlash()
    {
        _activeFlash = null;
        _flashActive = false;

        if (flashOverlayGroup != null) flashOverlayGroup.alpha = 0f;
        if (flashOverlayImage != null) flashOverlayImage.enabled = false;
    }

    private void SetFlashAlpha(float a)
    {
        if (flashOverlayGroup != null)
        {
            flashOverlayGroup.alpha = a;
        }
        else if (flashOverlayImage != null)
        {
            var c = flashOverlayImage.color;
            c.a = a;
            flashOverlayImage.color = c;
        }
    }

    private void SetExclamationVisible(bool on)
    {
        if (exclamationRoot != null) exclamationRoot.gameObject.SetActive(on);
        if (exclamationGroup != null) exclamationGroup.alpha = 0f; // 表示開始は必ず0
    }

    private static bool IsIconActive(Image img)
    {
        return img != null && img.gameObject.activeSelf;
    }

    private void SetIconVisible(Image img, CanvasGroup cg, bool on)
    {
        if (img == null) return;

        img.gameObject.SetActive(on);

        if (cg != null)
        {
            cg.alpha = 0f;
        }
        else
        {
            var c = img.color;
            c.a = 0f;
            img.color = c;
        }
    }

    private void ApplyIconAlpha(Image img, CanvasGroup cg, float a)
    {
        if (img == null) return;
        if (!img.gameObject.activeSelf) return;

        if (cg != null)
        {
            cg.alpha = a;
        }
        else
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }

    private IEnumerator FadeIconTo(Image img, CanvasGroup cg, float to, float dur)
    {
        if (img == null || !img.gameObject.activeSelf) yield break;

        if (cg != null)
        {
            yield return Fade(cg, cg.alpha, to, dur);
        }
        else
        {
            ApplyIconAlpha(img, null, to);
        }
    }

    private void ResetUI()
    {
        if (exclamationRoot) exclamationRoot.gameObject.SetActive(false);
        if (bannerRoot) bannerRoot.gameObject.SetActive(false);

        if (warningIconImage) warningIconImage.gameObject.SetActive(false);
        if (warningIconImage2) warningIconImage2.gameObject.SetActive(false);

        if (_warningSide != null) _warningSide.SetActive(false);

        if (exclamationGroup) exclamationGroup.alpha = 0f;
        if (bannerGroup) bannerGroup.alpha = 0f;
        if (warningIconGroup) warningIconGroup.alpha = 0f;
        if (warningIconGroup2) warningIconGroup2.alpha = 0f;
        if (warningSideGroup) warningSideGroup.alpha = 0f;

        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;
    }

    // -------------------------------
    // Coroutine utilities
    // -------------------------------

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
    {
        if (g == null) yield break;
        if (dur <= 0f) { g.alpha = to; yield break; }

        float t = 0f;
        g.alpha = from;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / dur);
            g.alpha = Mathf.Lerp(from, to, x);
            yield return null;
        }

        g.alpha = to;
    }

    private static IEnumerator SlideAndFade(
        RectTransform rt, CanvasGroup g,
        Vector2 posFrom, Vector2 posTo,
        float aFrom, float aTo,
        float dur)
    {
        if (rt == null || g == null) yield break;

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
            float e = x * x * (3f - 2f * x); // SmoothStep

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
