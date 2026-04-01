using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CreditsSequence : MonoBehaviour
{
    [Header("Logo (Game Logo)")]
    [SerializeField] private Image logoImage;
    [SerializeField] private RectTransform logoRect;
    [SerializeField] private float logoFadeInSeconds = 1.5f;
    [SerializeField] private float logoHoldSeconds = 0.8f;
    [SerializeField] private float logoMoveUpDeltaY = 500f;

    [Header("Credits")]
    [SerializeField] private RectTransform creditsRect;
    [SerializeField] private float creditsScrollSpeed = 80f;

    [Header("Team Logo (After Credits)")]
    [SerializeField] private Image teamLogoImage;
    [SerializeField] private float teamLogoFadeInSeconds = 0.3f;

    [Header("Thank You UI")]
    [SerializeField] private Image thankYouImage;
    [SerializeField] private float thankYouTargetY = 254.5f;
    [SerializeField] private float thankYouHoldSeconds = 2.0f;
    [SerializeField] private float thankYouFadeInSeconds = 0.25f;

    [Header("BackGround")]
    [SerializeField] private GameObject backGround = null;

    [Header("Fade to Black + Title")]
    [SerializeField] private Image fadeOverlayImage;
    [SerializeField] private float fadeToBlackSeconds = 1.0f;
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("BGM")]
    [SerializeField] private AudioSource bgm;
    [SerializeField] private float bgmStartVolume = 1f;
    [SerializeField] private float bgmFadeOutSeconds = 1.5f;

    // ================================
    // ★ 早送り
    // ================================
    [Header("Fast Forward")]
    [SerializeField] private bool enableFastForward = false;
    [SerializeField] private KeyCode fastForwardKey = KeyCode.Space;
    [SerializeField, Range(1f, 10f)] private float fastForwardTimeScale = 3f;
    [SerializeField] private bool holdToFastForward = true; // 押してる間だけ推奨

    private float _defaultTimeScale = 1f;
    private bool _isFastForwarding = false;

    // ★ThankYouが止まったら早送り受付終了
    private bool _canFastForward = true;

    public static CreditsSequence Instancs { get; private set; }

    private Vector2 _creditsStartPos;
    private Vector2 _teamLogoStartPos;
    private Vector2 _thankYouStartPos;
    private Vector2 _logoStartPos;

    void Awake()
    {
        Instancs = this;

        _defaultTimeScale = 1f;

        if (creditsRect) _creditsStartPos = creditsRect.anchoredPosition;
        if (teamLogoImage) _teamLogoStartPos = teamLogoImage.rectTransform.anchoredPosition;
        if (thankYouImage) _thankYouStartPos = thankYouImage.rectTransform.anchoredPosition;
        if (logoRect) _logoStartPos = logoRect.anchoredPosition;

        if (backGround) backGround.SetActive(false);
        if (creditsRect) creditsRect.gameObject.SetActive(false);

        SetAlpha(logoImage, 0f);

        if (teamLogoImage)
        {
            SetAlpha(teamLogoImage, 0f);
            teamLogoImage.gameObject.SetActive(false);
        }

        if (thankYouImage)
        {
            SetAlpha(thankYouImage, 0f);
            thankYouImage.gameObject.SetActive(false);
        }

        if (fadeOverlayImage)
        {
            SetAlpha(fadeOverlayImage, 0f);
        }
    }

    void Update()
    {
        if (!enableFastForward) return;
        if (!_canFastForward) return; // ★ThankYou停止後は早送り受付しない
        //クレジット画面に来たら
        if (!GameDirector.Instance.isCredits) return;

        if (holdToFastForward)
        {
            if (Input.GetKeyDown(fastForwardKey)) SetFastForward(true);
            if (Input.GetKeyUp(fastForwardKey)) SetFastForward(false);

            foreach(var pad in Gamepad.all)
            {
                if (pad.buttonWest.isPressed)
                {
                    SetFastForward(true);
                }
                else
                {
                    SetFastForward(false);
                }
            }

        }
        else
        {
            if (Input.GetKeyDown(fastForwardKey))
            {
                SetFastForward(!_isFastForwarding);
            }
        }
    }

    private void SetFastForward(bool enable)
    {
        _isFastForwarding = enable;

        Time.timeScale = enable ? fastForwardTimeScale : _defaultTimeScale;

        // Physics安定（任意）
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    private void ResetTimeScaleSafe()
    {
        _isFastForwarding = false;
        Time.timeScale = _defaultTimeScale;
        Time.fixedDeltaTime = 0.02f;
    }

    private void OnDisable()
    {
        ResetTimeScaleSafe();
    }

    private void OnDestroy()
    {
        ResetTimeScaleSafe();
    }

    public void ActiveCredits()
    {
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {

        // BGM開始
        if (bgm)
        {
            bgm.volume = Mathf.Clamp01(bgmStartVolume);
            if (!bgm.isPlaying) bgm.Play();
        }

        // 0
        if (backGround) backGround.SetActive(true);
        if (creditsRect) creditsRect.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.5f);

        // ★開始時に早送り受付ON＆timeScale戻す
        _canFastForward = true;
        enableFastForward = true;
        ResetTimeScaleSafe();

        // 位置リセット（連続再生でも安全）
        if (creditsRect) creditsRect.anchoredPosition = _creditsStartPos;
        if (teamLogoImage) teamLogoImage.rectTransform.anchoredPosition = _teamLogoStartPos;
        if (thankYouImage) thankYouImage.rectTransform.anchoredPosition = _thankYouStartPos;
        if (logoRect) logoRect.anchoredPosition = _logoStartPos;

        

        // 1) ゲームロゴ フェードイン
        yield return StartCoroutine(FadeImageAlpha(logoImage, 0f, 1f, logoFadeInSeconds));

        // 2) ロゴ表示しきってから静止
        if (logoHoldSeconds > 0f)
            yield return new WaitForSeconds(logoHoldSeconds);

        // 3) ロゴ上移動 + クレジットスクロール（同時）
        bool logoDone = false;
        bool creditsDone = false;

        StartCoroutine(MoveUpAtSpeed_WithDone(logoRect, logoMoveUpDeltaY, creditsScrollSpeed, () => logoDone = true));
        StartCoroutine(ScrollCreditsUp_UntilOffscreen_WithDone(creditsRect, creditsScrollSpeed, () => creditsDone = true));

        // TeamLogoも最初からスクロール開始（空白消し）
        bool teamLogoDone = false;
        if (teamLogoImage)
        {
            teamLogoImage.gameObject.SetActive(true);

            // 画面外でON → フェードイン（任意）
            yield return StartCoroutine(FadeImageAlpha(teamLogoImage, 0f, 1f, teamLogoFadeInSeconds));

            StartCoroutine(ScrollRectUp_UntilOffscreen_WithDone(
                teamLogoImage.rectTransform,
                creditsScrollSpeed,
                () => teamLogoDone = true
            ));
        }
        else
        {
            teamLogoDone = true;
        }

        // ThankYouも画面外で先にONして透明待機
        if (thankYouImage)
        {
            thankYouImage.gameObject.SetActive(true);
            SetAlpha(thankYouImage, 0f);
        }

        // ロゴ＆クレジットが終わるまで待つ
        while (!logoDone || !creditsDone)
            yield return null;

        // 4) クレジット終わったら整理
        if (creditsRect) creditsRect.gameObject.SetActive(false);
        if (logoRect) logoRect.gameObject.SetActive(false);

        // 5) チームロゴが完全に画面外へ抜けるまで待つ
        while (!teamLogoDone)
            yield return null;

        // 6) ThankYou：透明→フェードインして中央で止める（ここでBGMフェード）
        if (thankYouImage)
        {
            yield return StartCoroutine(FadeImageAlpha(thankYouImage, 0f, 1f, thankYouFadeInSeconds));

            yield return StartCoroutine(
                MoveToY_AndTriggerBgmFade(
                    thankYouImage.rectTransform,
                    thankYouTargetY,
                    creditsScrollSpeed,
                    bgm,
                    bgmFadeOutSeconds
                )
            );

            // ★ThankYouが中央で止まったら「早送り終了＆以降無効化」
            ResetTimeScaleSafe();
            _canFastForward = false;
        }

        // 7) 中央で静止
        if (thankYouHoldSeconds > 0f)
            yield return new WaitForSeconds(thankYouHoldSeconds);

        // 8) 黒フェード → タイトル
        if (fadeOverlayImage && !fadeOverlayImage.gameObject.activeSelf)
            fadeOverlayImage.gameObject.SetActive(true);

        yield return StartCoroutine(FadeImageAlpha(fadeOverlayImage, 0f, 1f, fadeToBlackSeconds));

        // 保険（シーン遷移前）
        ResetTimeScaleSafe();
        SceneManager.LoadScene(titleSceneName);
    }

    // ---- helpers ----

    static void SetAlpha(Image img, float a)
    {
        if (!img) return;
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    IEnumerator FadeImageAlpha(Image img, float from, float to, float seconds)
    {
        if (!img) yield break;

        if (seconds <= 0f)
        {
            SetAlpha(img, to);
            yield break;
        }

        float t = 0f;
        Color c = img.color;
        while (t < seconds)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / seconds);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    IEnumerator FadeOutBgm(AudioSource source, float seconds)
    {
        if (!source) yield break;

        float start = source.volume;
        if (seconds <= 0f)
        {
            source.volume = 0f;
            source.Stop();
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
    }

    IEnumerator MoveUpAtSpeed_WithDone(RectTransform rt, float deltaY, float speed, System.Action onDone)
    {
        if (!rt) { onDone?.Invoke(); yield break; }

        float startY = rt.anchoredPosition.y;
        float targetY = startY + deltaY;

        while (rt.anchoredPosition.y < targetY)
        {
            var p = rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            rt.anchoredPosition = p;
            yield return null;
        }

        var end = rt.anchoredPosition;
        end.y = targetY;
        rt.anchoredPosition = end;

        onDone?.Invoke();
    }

    IEnumerator ScrollCreditsUp_UntilOffscreen_WithDone(RectTransform rt, float speed, System.Action onDone)
    {
        if (!rt) { onDone?.Invoke(); yield break; }

        var corners = new Vector3[4];

        while (true)
        {
            var p = rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            rt.anchoredPosition = p;

            rt.GetWorldCorners(corners);
            bool allAbove = true;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                if (sp.y <= Screen.height)
                {
                    allAbove = false;
                    break;
                }
            }

            if (allAbove) break;
            yield return null;
        }

        onDone?.Invoke();
    }

    IEnumerator ScrollRectUp_UntilOffscreen_WithDone(RectTransform rt, float speed, System.Action onDone)
    {
        if (!rt) { onDone?.Invoke(); yield break; }

        var corners = new Vector3[4];

        while (true)
        {
            var p = rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            rt.anchoredPosition = p;

            rt.GetWorldCorners(corners);
            bool allAbove = true;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                if (sp.y <= Screen.height)
                {
                    allAbove = false;
                    break;
                }
            }

            if (allAbove) break;
            yield return null;
        }

        onDone?.Invoke();
    }

    IEnumerator MoveToY_AndTriggerBgmFade(RectTransform rt, float targetY, float speed, AudioSource bgm, float bgmFadeSeconds)
    {
        if (!rt) yield break;

        bool fadeStarted = false;

        while (rt.anchoredPosition.y < targetY)
        {
            var p = rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            rt.anchoredPosition = p;

            if (!fadeStarted && p.y >= targetY)
            {
                fadeStarted = true;
                if (bgm) StartCoroutine(FadeOutBgm(bgm, bgmFadeSeconds));
            }

            yield return null;
        }

        var end = rt.anchoredPosition;
        end.y = targetY;
        rt.anchoredPosition = end;

        if (!fadeStarted)
        {
            if (bgm) StartCoroutine(FadeOutBgm(bgm, bgmFadeSeconds));
        }
    }
}
