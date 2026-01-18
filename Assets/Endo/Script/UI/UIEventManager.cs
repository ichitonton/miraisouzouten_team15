using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private CanvasGroup warningSideGroup = null;
    [SerializeField, Min(0f)] private float warningSideFadeIn = 0.20f;
    [SerializeField, Min(0f)] private float warningSideFadeOut = 0.20f;

    // ================================
    // ★ Scene UI Retreat Settings（追加）
    // ================================

    public enum RetreatDir { Left, Right, Up, Down }

    [Header("Scene UI Retreat (hide original UIs by moving offscreen)")]
    [Tooltip("イベント演出中に画面外へ退避させたいUI（左へ）")]
    [SerializeField] private List<RectTransform> sceneUILeft = new();

    [Tooltip("イベント演出中に画面外へ退避させたいUI（右へ）")]
    [SerializeField] private List<RectTransform> sceneUIRight = new();

    [Tooltip("イベント演出中に画面外へ退避させたいUI（上へ）")]
    [SerializeField] private List<RectTransform> sceneUIUp = new();

    [Tooltip("イベント演出中に画面外へ退避させたいUI（下へ）")]
    [SerializeField] private List<RectTransform> sceneUIDown = new();

    [SerializeField, Min(0.01f)] private float sceneUIHideDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float sceneUIShowDuration = 0.25f;

    [Tooltip("画面外へ押し出す余白（大きめ推奨）")]
    [SerializeField, Min(0f)] private float offscreenMargin = 200f;

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

    // ★Scene UI retreat state
    private Coroutine _sceneUIRoutine;
    private bool _sceneUIHidden = false;

    private readonly Dictionary<RectTransform, Vector2> _sceneUIOriginalPos = new();
    private readonly Dictionary<RectTransform, RetreatDir> _sceneUIDir = new();

    public static UIEventManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (warningSideGroup == null && _warningSide != null)
            warningSideGroup = _warningSide.GetComponent<CanvasGroup>();

        BuildSceneUITargets(); // ★追加（方向辞書を作る）

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

        // ★イベント開始：元UIを画面外へ退避（Activeは触らない）
        yield return HideSceneUI();

        SetupWarningIconSingle(warningIcon);
        SetupWarningIconDouble(null, null);
        SetupFlash(flash);

        bool wantWarningSide = (warningIcon != null);
        StartWarningSide(wantWarningSide);

        SetExclamationVisible(true);

        yield return StartCoroutine(BlinkCoreRoutine());
        yield return StartCoroutine(BannerRoutine(message));

        yield return StopWarningSide();

        // ★イベント終了：元UIを元の位置へ戻す
        yield return ShowSceneUI();

        _running = null;
    }

    private IEnumerator SequenceDouble(string messageCombined, Sprite iconA, Sprite iconB, ScreenFlashSetting flashCombined)
    {
        ResetUI();
        StopFlash();

        // ★イベント開始：元UIを画面外へ退避
        yield return HideSceneUI();

        SetupWarningIconSingle(null);
        SetupWarningIconDouble(iconA, iconB);
        SetupFlash(flashCombined);

        bool wantWarningSide = (iconA != null || iconB != null);
        StartWarningSide(wantWarningSide);

        SetExclamationVisible(true);

        yield return StartCoroutine(BlinkCoreRoutine());
        yield return StartCoroutine(BannerRoutine(messageCombined));

        yield return StopWarningSide();

        // ★イベント終了：元UI復帰
        yield return ShowSceneUI();

        _running = null;
    }

    // -------------------------------
    // Core routines
    // -------------------------------

    private IEnumerator BlinkCoreRoutine()
    {
        float t = 0f;
        float w = exclamationBlinkSpeed * Mathf.PI * 2f;

        while (t < exclamationBlinkDuration)
        {
            float s = (1f - Mathf.Cos(t * w)) * 0.5f;
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

        if (exclamationGroup != null)
            yield return Fade(exclamationGroup, exclamationGroup.alpha, 0f, exclamationFadeOut);

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
    // ★ Scene UI Retreat（追加）
    // -------------------------------

    /// <summary>
    /// Inspectorの4方向リストから、UI->方向 の辞書を作る
    /// </summary>
    private void BuildSceneUITargets()
    {
        _sceneUIDir.Clear();

        AddDir(sceneUILeft, RetreatDir.Left);
        AddDir(sceneUIRight, RetreatDir.Right);
        AddDir(sceneUIUp, RetreatDir.Up);
        AddDir(sceneUIDown, RetreatDir.Down);

        void AddDir(List<RectTransform> list, RetreatDir dir)
        {
            if (list == null) return;
            foreach (var rt in list)
            {
                if (rt == null) continue;

                if (_sceneUIDir.ContainsKey(rt))
                {
                    // 同じUIが複数方向に入ってたら最初優先（事故予防）
                    Debug.LogWarning($"[UIEventManager] SceneUI '{rt.name}' is registered multiple times. First direction is used.");
                    continue;
                }
                _sceneUIDir.Add(rt, dir);
            }
        }
    }

    private IEnumerator HideSceneUI()
    {
        if (_sceneUIHidden) yield break;
        if (_sceneUIDir.Count == 0) yield break;

        // 途中の移動が残ってるなら止める
        if (_sceneUIRoutine != null) StopCoroutine(_sceneUIRoutine);

        // 元位置保存（初回のみ）
        foreach (var kv in _sceneUIDir)
        {
            var rt = kv.Key;
            if (rt == null) continue;
            if (!_sceneUIOriginalPos.ContainsKey(rt))
                _sceneUIOriginalPos[rt] = rt.anchoredPosition;
        }

        _sceneUIRoutine = StartCoroutine(CoSlideSceneUI(hide: true, sceneUIHideDuration));
        yield return _sceneUIRoutine;

        _sceneUIHidden = true;
        _sceneUIRoutine = null;
    }

    private IEnumerator ShowSceneUI()
    {
        if (!_sceneUIHidden) yield break;
        if (_sceneUIDir.Count == 0) yield break;

        if (_sceneUIRoutine != null) StopCoroutine(_sceneUIRoutine);

        _sceneUIRoutine = StartCoroutine(CoSlideSceneUI(hide: false, sceneUIShowDuration));
        yield return _sceneUIRoutine;

        _sceneUIHidden = false;
        _sceneUIRoutine = null;
    }

    private IEnumerator CoSlideSceneUI(bool hide, float duration)
    {
        // Canvasサイズ取得（解像度変化してもOK）
        var canvasRect = GetRootCanvasRect();
        float w = canvasRect.x;
        float h = canvasRect.y;

        // 開始/目標位置を作る
        var rts = new List<RectTransform>(_sceneUIDir.Count);
        var start = new List<Vector2>(_sceneUIDir.Count);
        var target = new List<Vector2>(_sceneUIDir.Count);

        foreach (var kv in _sceneUIDir)
        {
            var rt = kv.Key;
            if (rt == null) continue;

            rts.Add(rt);
            start.Add(rt.anchoredPosition);

            Vector2 origin = _sceneUIOriginalPos.TryGetValue(rt, out var o) ? o : rt.anchoredPosition;

            if (!hide)
            {
                target.Add(origin);
                continue;
            }

            // hide時：方向に応じて画面外へ押し出す
            Vector2 to = origin;
            switch (kv.Value)
            {
                case RetreatDir.Left: to.x = origin.x - (w + offscreenMargin); break;
                case RetreatDir.Right: to.x = origin.x + (w + offscreenMargin); break;
                case RetreatDir.Up: to.y = origin.y + (h + offscreenMargin); break;
                case RetreatDir.Down: to.y = origin.y - (h + offscreenMargin); break;
            }
            target.Add(to);
        }

        if (duration <= 0f)
        {
            for (int i = 0; i < rts.Count; i++)
                if (rts[i] != null) rts[i].anchoredPosition = target[i];
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);

            // SmoothStep
            float e = x * x * (3f - 2f * x);

            for (int i = 0; i < rts.Count; i++)
            {
                if (rts[i] == null) continue;
                rts[i].anchoredPosition = Vector2.Lerp(start[i], target[i], e);
            }

            yield return null;
        }

        for (int i = 0; i < rts.Count; i++)
            if (rts[i] != null) rts[i].anchoredPosition = target[i];
    }

    private Vector2 GetRootCanvasRect()
    {
        // なるべくRootCanvasを取りに行く（UI階層が複雑でも安定）
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas != null)
        {
            var rt = canvas.rootCanvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                var size = rt.rect.size;
                return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            }
        }

        // fallback（最悪でも動く）
        return new Vector2(Screen.width, Screen.height);
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
            _warningSide.GetComponent<Image>().color = s.flashColor;
        }

        SetFlashAlpha(0f);
    }

    // -------------------------------
    // WarningSide control
    // -------------------------------

    private void StartWarningSide(bool on)
    {
        if (_warningSide == null || warningSideGroup == null)
        {
            if (_warningSide != null) _warningSide.SetActive(on);
            return;
        }

        if (_warningSideFadeRoutine != null) StopCoroutine(_warningSideFadeRoutine);
        _warningSideFadeRoutine = null;

        if (!on)
        {
            _warningSide.SetActive(false);
            warningSideGroup.alpha = 0f;
            return;
        }

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
        if (exclamationGroup != null) exclamationGroup.alpha = 0f;
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
