using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UIイベント演出マネージャー
/// - 中央の「!」＋（任意で）中央ワーニングアイコンを表示
/// - その点滅（Sin/Cos）に同期して、画面全体のフラッシュも点滅させる（任意）
/// - 最後に上からメッセージバナーをスライド表示
///
/// 設計方針（バグ予防）
/// - “表示/非表示”は基本 CanvasGroup.alpha と enabled で制御
/// - ResetUI() では StopFlash() を呼ばない（以前のバグ：起動直後に消える）
/// - Play() は常に1本だけ（コルーチン重複を止める）
///
/// ※ ScreenFlashSetting は EventDatabase から渡される設定（色/画像/maxAlpha など）
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
    [SerializeField] private Image warningIconImage;          // 中央ワーニング画像（任意）
    [SerializeField] private CanvasGroup warningIconGroup;    // あるとα制御が安定（任意）

    [Header("上から出るテキスト")]
    [SerializeField] private RectTransform bannerRoot;        // バナーのルート
    [SerializeField] private CanvasGroup bannerGroup;         // バナーのα制御用
    [SerializeField] private TMP_Text bannerText;             // バナーのテキスト

    [Header("Screen Flash Overlay (optional)")]
    [SerializeField] private Image flashOverlayImage;         // 画面全体のオーバーレイImage（任意）
    [SerializeField] private CanvasGroup flashOverlayGroup;   // あるとα制御が安定（任意）

    // ================================
    // Timing settings
    // ================================

    [Header("Timing")]
    [SerializeField] private float exclamationFadeIn = 0.35f;        // 「!」のフェードイン時間
    [SerializeField] private float exclamationBlinkDuration = 1.2f;  // 点滅する合計時間
    [SerializeField] private float exclamationBlinkSpeed = 2.0f;     // 点滅速度（周波数）
    [SerializeField] private float exclamationFadeOut = 0.25f;       // 「!」のフェードアウト時間

    [SerializeField] private float bannerSlideIn = 0.35f;            // バナー出現スライド時間
    [SerializeField] private float bannerHold = 1.2f;                // バナー表示維持時間
    [SerializeField] private float bannerSlideOut = 0.25f;           // バナー退場スライド時間

    [Header("Banner positions (anchored)")]
    [SerializeField] private Vector2 bannerHiddenPos = new Vector2(0, 140); // 画面上に隠す位置
    [SerializeField] private Vector2 bannerShownPos = new Vector2(0, 40);  // 表示位置

    // ================================
    // Runtime state
    // ================================

    // フラッシュ同期用の設定を保持（点滅ループ内で参照してa更新する）
    private ScreenFlashSetting _activeFlash;
    private bool _flashActive;

    // 実行中演出のコルーチン（常に1本にする）
    private Coroutine _running;

    // Singleton（既存設計に合わせている）
    public static UIEventManager Instance { get; private set; }

    private void Awake()
    {
        // 既存のシングルトンパターン
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 初期状態は確実に非表示にする
        ResetUI();
        StopFlash(); // ResetUIに入れない（過去に“開始直後に消える”原因になった）
    }

    // -------------------------------
    // Public API
    // -------------------------------

    /// <summary>
    /// 互換用：メッセージだけ再生（アイコン/フラッシュなし）
    /// </summary>
    public void Play(string message) => Play(message, null, null);

    /// <summary>
    /// メッセージ＋中央アイコン＋画面フラッシュを含めて演出再生する
    /// - warningIcon: 中央に出す画像（任意）
    /// - flash: 画面フラッシュ設定（任意）
    /// </summary>
    public void Play(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        Debug.Log($"[UIEventManager] Play frame={Time.frameCount} msg={message}");
        // 重複事故防止：常に前の演出を止めてから新しい演出を開始する
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Sequence(message, warningIcon, flash));
    }

    // -------------------------------
    // Main sequence
    // -------------------------------

    /// <summary>
    /// UI演出のメインシーケンス
    /// 1) 中央「!」フェードイン
    /// 2) 点滅（アイコン＆フラッシュも同期）
    /// 3) フェードアウト
    /// 4) メッセージバナーをスライド表示 →  → 退場
    /// </summary>
    private IEnumerator Sequence(string message, Sprite warningIcon, ScreenFlashSetting flash)
    {
        // シーケンス開始時にUIを初期化
        // ※ここで StopFlash も呼んで、前回のフラッシュ残りを消す（状態もリセット）
        ResetUI();
        StopFlash();

        // ---------------------------
        // 1) セットアップ
        // ---------------------------
        // 中央アイコン準備（指定がある場合だけ表示する）
        SetupWarningIcon(warningIcon);

        // フラッシュ準備（指定がある場合だけ enabled=true になる）
        SetupFlash(flash);

        // ---------------------------
        // 2) 点滅（完全同期）
        // ---------------------------
        // cos方式にすることで、t=0の時に s=0 から始まる（初フレームの“ポップ”を抑える）
        float t = 0f;
        float w = exclamationBlinkSpeed * Mathf.PI * 2f;

        while (t < exclamationBlinkDuration)
        {
            // 0..1。t=0で0スタート
            float s = (1f - Mathf.Cos(t * w)) * 0.5f;

            // 次フレームへ
            t += Time.unscaledDeltaTime;

            // 「!」とアイコンのa（0.35 - 1.0を往復）
            float warnA = Mathf.Lerp(0.35f, 1f, s);

            if (exclamationGroup) exclamationGroup.alpha = warnA;

            // アイコン側（CanvasGroupがあればそっち、なければImageのαを直接いじる）
            if (warningIconGroup != null)
            {
                warningIconGroup.alpha = warnA;
            }
            else if (warningIconImage && warningIconImage.gameObject.activeSelf)
            {
                SetWarningIconAlpha(warnA);
            }

            // 画面フラッシュ（ワーニング点滅と完全同期）
            // - 0..maxAlpha を往復
            if (_flashActive && _activeFlash != null)
            {
                float flashA = Mathf.Lerp(0f, Mathf.Clamp01(_activeFlash.maxAlpha), s);
                SetFlashAlpha(flashA);
            }

            yield return null;
        }

        // ---------------------------
        // 3) フェードアウト
        // ---------------------------
        if (exclamationGroup) yield return Fade(exclamationGroup, exclamationGroup.alpha, 0f, exclamationFadeOut);

        if (warningIconGroup != null)
        {
            yield return Fade(warningIconGroup, warningIconGroup.alpha, 0f, exclamationFadeOut);
        }
        else if (warningIconImage && warningIconImage.gameObject.activeSelf)
        {
            SetWarningIconAlpha(0f);
        }

        // フラッシュも確実に停止（enabled=false + 状態リセット）
        StopFlash();

        // ルートを非アクティブ化
        if (exclamationRoot) exclamationRoot.gameObject.SetActive(false);
        if (warningIconImage) warningIconImage.gameObject.SetActive(false);

        // ---------------------------
        // 4) バナー表示
        // ---------------------------
        if (bannerText) bannerText.text = message;

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

        // 終了
        _running = null;
    }

    // -------------------------------
    // Setup helpers
    // -------------------------------

    /// <summary>
    /// フラッシュの見た目をセットし、同期点滅の“準備”だけする
    /// - ここでは enabled=true にして、αは0にしておく
    /// - 点滅は Sequence 内の while で同期更新する
    /// </summary>
    private void SetupFlash(ScreenFlashSetting s)
    {
        _activeFlash = s;
        _flashActive = (s != null && flashOverlayImage != null);

        if (!_flashActive) return;

        // 画面フラッシュを表示可能状態にする
        flashOverlayImage.enabled = true;

        // 画像があれば画像、なければ色
        // ※ αは SetFlashAlpha で制御するのでここではベースカラーだけ
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

        // 開始時は透明（ここから点滅ループで上げる）
        SetFlashAlpha(0f);
    }

    /// <summary>
    /// 中央ワーニングアイコンのセットアップ
    /// - icon が null なら非表示
    /// - icon があるなら sprite を設定して表示（αは0から開始）
    /// </summary>
    private void SetupWarningIcon(Sprite icon)
    {
        if (warningIconImage == null) return;

        if (icon == null)
        {
            warningIconImage.gameObject.SetActive(false);
            return;
        }

        warningIconImage.sprite = icon;
        warningIconImage.gameObject.SetActive(true);

        // 開始時は透明
        if (warningIconGroup != null) warningIconGroup.alpha = 0f;
        else SetWarningIconAlpha(0f);
    }

    // -------------------------------
    // Stop / Apply helpers
    // -------------------------------

    /// <summary>
    /// フラッシュ停止（必ず状態もリセットする）
    /// - enabled=false にして描画を止める
    /// - CanvasGroupがあればαも0に戻す
    /// </summary>
    private void StopFlash()
    {
        _activeFlash = null;
        _flashActive = false;

        if (flashOverlayGroup != null) flashOverlayGroup.alpha = 0f;
        if (flashOverlayImage != null) flashOverlayImage.enabled = false;
    }

    /// <summary>
    /// フラッシュのαを適用する（CanvasGroup優先）
    /// </summary>
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

    /// <summary>
    /// 中央アイコンのαを適用する（CanvasGroupが無い場合に使用）
    /// </summary>
    private void SetWarningIconAlpha(float a)
    {
        if (warningIconImage == null) return;
        var c = warningIconImage.color;
        c.a = a;
        warningIconImage.color = c;
    }

    /// <summary>
    /// UIを初期状態に戻す
    /// - SetActive/alpha/位置などを初期化する
    /// - StopFlashは呼ばない（Sequence開始時にだけStopFlashする）
    /// </summary>
    private void ResetUI()
    {
        if (exclamationRoot) exclamationRoot.gameObject.SetActive(false);
        if (bannerRoot) bannerRoot.gameObject.SetActive(false);
        if (warningIconImage) warningIconImage.gameObject.SetActive(false);

        if (exclamationGroup) exclamationGroup.alpha = 0f;
        if (bannerGroup) bannerGroup.alpha = 0f;
        if (warningIconGroup) warningIconGroup.alpha = 0f;

        if (bannerRoot) bannerRoot.anchoredPosition = bannerHiddenPos;
    }

    // -------------------------------
    // Coroutine utilities
    // -------------------------------

    /// <summary>
    /// CanvasGroupのフェード
    /// </summary>
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

    /// <summary>
    /// RectTransformのスライド＋CanvasGroupのフェードを同時に行う
    /// </summary>
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

            // SmoothStepのイージング（少し気持ちいい動き）
            float e = x * x * (3f - 2f * x);

            rt.anchoredPosition = Vector2.Lerp(posFrom, posTo, e);
            g.alpha = Mathf.Lerp(aFrom, aTo, e);

            yield return null;
        }

        rt.anchoredPosition = posTo;
        g.alpha = aTo;
    }

    /// <summary>
    /// unscaled時間で待つ（ポーズ中でも演出を動かしたい時に使う）
    /// </summary>
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
