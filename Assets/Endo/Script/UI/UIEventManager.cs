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
    [SerializeField] private RectTransform exclamationRoot;   // 「!」のルート
    [SerializeField] private CanvasGroup exclamationGroup;    // 「!」のa制御用

    [Header("Center Warning Icon (optional)")]
    [SerializeField] private Image warningIconImage;          // ワーニングアイコン(左/単体用)
    [SerializeField] private CanvasGroup warningIconGroup;    // α制御(任意)

    [Header("Center Warning Icon #2 (optional / Double)")]
    [SerializeField] private Image warningIconImage2;         // ★同時用（右側）
    [SerializeField] private CanvasGroup warningIconGroup2;   // ★同時用 α制御(任意)
    [SerializeField, Min(0f)] private float warningOffsetX = 60f; // 左右にずらす距離（anchored）

    [Header("上から出るテキスト")]
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private TMP_Text bannerText;

    [Header("Screen Flash Overlay (optional)")]
    [SerializeField] private Image flashOverlayImage;
    [SerializeField] private CanvasGroup flashOverlayGroup;

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

    public static UIEventManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 起動直後は確実に「何も出ない」状態に固定する
        ResetUI();
        StopFlash();
    }

    // -------------------------------
    // Public API
    // -------------------------------

    /// <summary>
    /// 互換：メッセージだけ
    /// </summary>
    public void Play(string message) => Play(message, null, null);

    /// <summary>
    /// 単発：メッセージ＋アイコン1つ＋フラッシュ
    /// </summary>
    public void Play(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SequenceSingle(message, warningIcon, flash));
    }

    /// <summary>
    /// ★同時：メッセージ（結合済み）＋アイコン2つ＋フラッシュ（合成済み）
    /// </summary>
    public void PlayDouble(string messageCombined, Sprite iconA, Sprite iconB, ScreenFlashSetting flashCombined)
    {
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(SequenceDouble(messageCombined, iconA, iconB, flashCombined));
    }

    // -------------------------------
    // Main sequences
    // -------------------------------

    /// <summary>
    /// 単発シーケンス
    /// </summary>
    private IEnumerator SequenceSingle(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        ResetUI();
        StopFlash();

        // まず「表示対象を確定して透明」にする（初フレームのポップ予防）
        SetupWarningIconSingle(warningIcon);
        SetupWarningIconDouble(null, null); // 2個目側は必ず消す
        SetupFlash(flash);

        // ここで「!」も先に表示状態へ（alphaはこの時点で0）
        SetExclamationVisible(true);

        // 点滅ループ（cosで0スタート）
        yield return StartCoroutine(BlinkCoreRoutine());

        // バナー
        yield return StartCoroutine(BannerRoutine(message));

        _running = null;
    }

    /// <summary>
    /// ★同時シーケンス（アイコン2つ表示）
    /// </summary>
    private IEnumerator SequenceDouble(string messageCombined, Sprite iconA, Sprite iconB, ScreenFlashSetting flashCombined)
    {
        ResetUI();
        StopFlash();

        // まず「2つのアイコンを表示対象として確定→透明」にする（初フレームのポップ予防）
        SetupWarningIconSingle(null);              // 単発側は使わない（消す）
        SetupWarningIconDouble(iconA, iconB);      // 2つ表示
        SetupFlash(flashCombined);

        // 「!」表示（alphaは0）
        SetExclamationVisible(true);

        // 点滅ループ（!・2アイコン・フラッシュが完全同期）
        yield return StartCoroutine(BlinkCoreRoutine());

        // バナー（結合済み文字列）
        yield return StartCoroutine(BannerRoutine(messageCombined));

        _running = null;
    }

    // -------------------------------
    // Core routines
    // -------------------------------

    /// <summary>
    /// 「!」/アイコン/フラッシュの点滅シーケンス（完全同期）
    /// - フェードイン → 点滅 → フェードアウト までをまとめて処理
    /// - Single/Double どちらでも共通に使える
    /// </summary>
    private IEnumerator BlinkCoreRoutine()
    {
        //// 1) 「!」フェードイン（存在する場合）
        //if (exclamationGroup != null)
        //    yield return Fade(exclamationGroup, 0f, 1f, exclamationFadeIn);

        //// ★ワーニングアイコンも“同じタイミング”でフェードインしておくと初動が綺麗
        //// （表示対象だけをフェードする）
        //if (IsIconActive(warningIconImage))
        //    yield return FadeIconTo(warningIconImage, warningIconGroup, 1f, exclamationFadeIn);

        //if (IsIconActive(warningIconImage2))
        //    yield return FadeIconTo(warningIconImage2, warningIconGroup2, 1f, exclamationFadeIn);

        // 2) 点滅（cosで0スタート）
        float t = 0f;
        float w = exclamationBlinkSpeed * Mathf.PI * 2f;

        while (t < exclamationBlinkDuration)
        {
            float s = (1f - Mathf.Cos(t * w)) * 0.5f; // 0..1, t=0で0
            t += Time.unscaledDeltaTime;

            // ! / icon の点滅α
            float warnA = Mathf.Lerp(0.35f, 1f, s);

            if (exclamationGroup != null) exclamationGroup.alpha = warnA;

            ApplyIconAlpha(warningIconImage, warningIconGroup, warnA);
            ApplyIconAlpha(warningIconImage2, warningIconGroup2, warnA);

            // フラッシュ同期（0..maxAlpha）
            if (_flashActive && _activeFlash != null)
            {
                float flashA = Mathf.Lerp(0f, Mathf.Clamp01(_activeFlash.maxAlpha), s);
                SetFlashAlpha(flashA);
            }

            yield return null;
        }

        // 3) フェードアウト（! / icon）
        if (exclamationGroup != null)
            yield return Fade(exclamationGroup, exclamationGroup.alpha, 0f, exclamationFadeOut);

        // アイコンは「表示しているものだけ」落とす
        if (IsIconActive(warningIconImage))
            yield return FadeIconTo(warningIconImage, warningIconGroup, 0f, exclamationFadeOut);
        if (IsIconActive(warningIconImage2))
            yield return FadeIconTo(warningIconImage2, warningIconGroup2, 0f, exclamationFadeOut);

        // フラッシュは確実に停止
        StopFlash();

        // ルートを非表示
        SetExclamationVisible(false);
        SetIconVisible(warningIconImage, warningIconGroup, false);
        SetIconVisible(warningIconImage2, warningIconGroup2, false);
    }

    /// <summary>
    /// バナー（上からスライドして表示→待機→戻す）
    /// </summary>
    private IEnumerator BannerRoutine(string message)
    {
        if (bannerText) bannerText.text = message ?? "";

        if (bannerRoot) bannerRoot.gameObject.SetActive(true);
        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;
        if (bannerGroup) bannerGroup.alpha = 0f;

        // 出現
        yield return SlideAndFade(bannerRoot, bannerGroup, bannerHiddenPos, bannerShownPos, 0f, 1f, bannerSlideIn);

        // 維持
        yield return WaitUnscaled(bannerHold);

        // 退場
        yield return SlideAndFade(bannerRoot, bannerGroup, bannerShownPos, bannerHiddenPos, 1f, 0f, bannerSlideOut);

        if (bannerRoot) bannerRoot.gameObject.SetActive(false);
    }

    // -------------------------------
    // Setup helpers
    // -------------------------------

    /// <summary>
    /// 単発アイコンの準備
    /// - iconがnullなら確実に消す
    /// - iconがあるなら表示してalpha=0に固定
    /// </summary>
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

        // 初期αは0固定（初フレームの“チラ見え”を潰す）
        ApplyIconAlpha(warningIconImage, warningIconGroup, 0f);
    }

    /// <summary>
    /// ★同時アイコンの準備（2つ）
    /// - 片方がnullならその側は出さない
    /// - 出す場合は左右に少しずらす
    /// - alpha=0固定
    /// </summary>
    private void SetupWarningIconDouble(Sprite iconA, Sprite iconB)
    {
        // 2個目用が未設定なら何もしない（安全）
        // ※同時演出を使うなら Inspector で warningIconImage2 を必ず入れるの推奨
        if (warningIconImage2 == null)
        {
            // 片側しか出せないので、Aだけ単発側に出す（保険）
            SetupWarningIconSingle(iconA);
            return;
        }

        // 左（warningIconImage）をAに、右（warningIconImage2）をBに割り当てる
        SetupIconAt(warningIconImage, warningIconGroup, iconA, -warningOffsetX);
        SetupIconAt(warningIconImage2, warningIconGroup2, iconB, +warningOffsetX);
    }

    /// <summary>
    /// 指定したImageにアイコンをセットして表示/非表示・位置・alphaを初期化
    /// </summary>
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

        // anchoredPositionを左右にずらす（RectTransformがある前提）
        if (img.rectTransform != null)
        {
            var p = img.rectTransform.anchoredPosition;
            p.x = offsetX;
            img.rectTransform.anchoredPosition = p;
        }

        // 初期α0（初フレームのチラ見え防止）
        ApplyIconAlpha(img, cg, 0f);
    }

    /// <summary>
    /// フラッシュ準備（enabled=true、alpha=0にして点滅はBlinkで同期）
    /// </summary>
    private void SetupFlash(ScreenFlashSetting s)
    {
        _activeFlash = s;
        _flashActive = (s != null && flashOverlayImage != null);

        if (!_flashActive) return;

        flashOverlayImage.enabled = true;

        // overlaySpriteがあれば画像、なければ色
        if (s.overlaySprite != null)
        {
            flashOverlayImage.sprite = s.overlaySprite;
            flashOverlayImage.color = Color.white; // αはSetFlashAlphaで
        }
        else
        {
            flashOverlayImage.sprite = null;
            flashOverlayImage.color = s.flashColor;
        }

        SetFlashAlpha(0f);
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
        if (exclamationGroup != null && !on) exclamationGroup.alpha = 0f;
        if (exclamationGroup != null && on) exclamationGroup.alpha = 0f; // 表示開始は必ず0から
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
            cg.alpha = on ? 0f : 0f; // onでもまず0固定
        }
        else
        {
            // CanvasGroupが無い場合はImageのαを直接操作
            var c = img.color;
            c.a = 0f;
            img.color = c;
        }
    }

    /// <summary>
    /// アイコンαを適用（CanvasGroup優先）
    /// </summary>
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

    /// <summary>
    /// アイコンを指定αまでフェード（CanvasGroupが無い場合は即反映）
    /// </summary>
    private IEnumerator FadeIconTo(Image img, CanvasGroup cg, float to, float dur)
    {
        if (img == null || !img.gameObject.activeSelf) yield break;

        if (cg != null)
        {
            yield return Fade(cg, cg.alpha, to, dur);
        }
        else
        {
            // CanvasGroupが無いなら即反映（シンプルにする）
            ApplyIconAlpha(img, null, to);
            yield break;
        }
    }

    /// <summary>
    /// UIを初期状態に戻す
    /// - StopFlashは呼ばない（Sequence開始時にだけStopFlash）
    /// </summary>
    private void ResetUI()
    {
        // 非表示
        if (exclamationRoot) exclamationRoot.gameObject.SetActive(false);
        if (bannerRoot) bannerRoot.gameObject.SetActive(false);

        if (warningIconImage) warningIconImage.gameObject.SetActive(false);
        if (warningIconImage2) warningIconImage2.gameObject.SetActive(false);

        // α初期化
        if (exclamationGroup) exclamationGroup.alpha = 0f;
        if (bannerGroup) bannerGroup.alpha = 0f;
        if (warningIconGroup) warningIconGroup.alpha = 0f;
        if (warningIconGroup2) warningIconGroup2.alpha = 0f;

        // 位置初期化
        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;

        // アイコンの位置は SetupIconAt で入れるのでここでは触らない
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
