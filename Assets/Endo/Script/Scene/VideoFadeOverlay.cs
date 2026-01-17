using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoFadeOverlay : MonoBehaviour
{
    public static VideoFadeOverlay Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage transitionImage;

    [Header("Clips (recommended: separate Out / In)")]
    [SerializeField] private VideoClip fadeOutClip;
    [SerializeField] private VideoClip fadeInClip;

    [Header("Defaults")]
    [SerializeField, Min(0.05f)] private float defaultOutDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float defaultInDuration = 0.35f;
    [SerializeField, Min(0f)] private float defaultHoldSeconds = 0.2f;

    [Header("Behavior")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool blockInputWhileVisible = true;

    private CanvasGroup _cg;
    private Coroutine _co;
    private int _playToken = 0;

    public bool IsBusy { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        if (transitionImage != null)
        {
            transitionImage.gameObject.SetActive(false);

            // 入力ブロックしたい場合は CanvasGroup を追加して制御
            _cg = transitionImage.GetComponentInParent<CanvasGroup>();
            if (_cg == null) _cg = transitionImage.gameObject.GetComponent<CanvasGroup>();
            if (_cg == null) _cg = transitionImage.gameObject.AddComponent<CanvasGroup>();

            _cg.alpha = 1f;
            SetInputBlock(false);
        }

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =========================================================
    // Public API（外から使いやすい形）
    // =========================================================
    public void Cancel()
    {
        _playToken++;
        if (_co != null) StopCoroutine(_co);
        _co = null;

        IsBusy = false;

        if (videoPlayer != null)
            videoPlayer.Stop();

        Hide();
    }

    public void PlayFadeOutOnly(float? outDuration = null)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeOutOnly(token, outDuration ?? defaultOutDuration));
    }

    public void PlayFadeInOnly(float? inDuration = null)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeInOnly(token, inDuration ?? defaultInDuration));
    }

    /// <summary>
    /// Out → Hold → In のシーケンス（演出だけ）
    /// </summary>
    public void PlayFadeSequence(float? outDuration = null, float? holdSeconds = null, float? inDuration = null)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeSequence(token,
            outDuration ?? defaultOutDuration,
            holdSeconds ?? defaultHoldSeconds,
            inDuration ?? defaultInDuration));
    }

    /// <summary>
    /// Scene遷移などで使いやすい：Outして最終フレーム保持→Hold（ここで止められる）
    /// </summary>
    public IEnumerator CoFadeOutAndHold(float outDuration, float holdSeconds)
    {
        int token = ++_playToken;
        IsBusy = true;

        Show();
        yield return PlayClipHoldLastFrame(token, fadeOutClip, outDuration);

        if (!IsTokenAlive(token)) yield break;

        if (holdSeconds > 0f)
            yield return WaitRealtimeCancelable(token, holdSeconds);

        // この時点で「最終フレーム保持」のまま止まってる
    }

    /// <summary>
    /// Hold解除して In を流し、終わったら消す
    /// </summary>
    public IEnumerator CoFadeInAndHide(float inDuration)
    {
        int token = ++_playToken;
        IsBusy = true;

        Show();
        yield return PlayClipAndHide(token, fadeInClip, inDuration);

        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    // =========================================================
    // Core Routines
    // =========================================================
    private IEnumerator CoFadeOutOnly(int token, float outDuration)
    {
        IsBusy = true;
        Show();

        yield return PlayClipHoldLastFrame(token, fadeOutClip, outDuration);

        if (!IsTokenAlive(token)) yield break;

        // “最終フレーム保持”したまま終わる（必要なら外で Cancel or FadeIn を呼ぶ）
        IsBusy = false;
    }

    private IEnumerator CoFadeInOnly(int token, float inDuration)
    {
        IsBusy = true;
        Show();

        yield return PlayClipAndHide(token, fadeInClip, inDuration);

        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    private IEnumerator CoFadeSequence(int token, float outDuration, float holdSeconds, float inDuration)
    {
        IsBusy = true;
        Show();

        // Out → 最終フレーム保持
        yield return PlayClipHoldLastFrame(token, fadeOutClip, outDuration);
        if (!IsTokenAlive(token)) yield break;

        // Hold
        if (holdSeconds > 0f)
            yield return WaitRealtimeCancelable(token, holdSeconds);
        if (!IsTokenAlive(token)) yield break;

        // In → 終了後 Hide
        yield return PlayClipAndHide(token, fadeInClip, inDuration);
        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    // =========================================================
    // Clip Play Helpers（安定性重視）
    // =========================================================
    private IEnumerator PlayClipHoldLastFrame(int token, VideoClip clip, float targetDuration)
    {
        if (!EnsureReady())
            yield break;

        if (clip == null)
        {
            Debug.LogWarning("[VideoFadeOverlay] FadeOut clip is null.");
            yield break;
        }

        SetupClipSpeed(clip, targetDuration);

        // Prepare
        videoPlayer.Prepare();
        yield return WaitPrepared(token, 3.0f);
        if (!IsTokenAlive(token)) yield break;

        bool ended = false;
        void OnEnd(VideoPlayer vp) => ended = true;

        videoPlayer.loopPointReached += OnEnd;
        videoPlayer.Play();

        // 完了待ち（ended が一番安定）
        while (!ended)
        {
            if (!IsTokenAlive(token))
            {
                videoPlayer.loopPointReached -= OnEnd;
                yield break;
            }
            yield return null;
        }

        videoPlayer.loopPointReached -= OnEnd;

        // ★最終フレーム保持
        videoPlayer.Pause();

        // clip.frameCount が取れる場合は最後フレームに寄せる（環境差対策）
        if (clip.frameCount > 0)
            videoPlayer.frame = (long)(clip.frameCount - 1);
    }

    private IEnumerator PlayClipAndHide(int token, VideoClip clip, float targetDuration)
    {
        if (!EnsureReady())
            yield break;

        if (clip == null)
        {
            Debug.LogWarning("[VideoFadeOverlay] FadeIn clip is null.");
            Hide();
            yield break;
        }

        SetupClipSpeed(clip, targetDuration);

        videoPlayer.Prepare();
        yield return WaitPrepared(token, 3.0f);
        if (!IsTokenAlive(token)) yield break;

        bool ended = false;
        void OnEnd(VideoPlayer vp) => ended = true;

        videoPlayer.loopPointReached += OnEnd;
        videoPlayer.Play();

        while (!ended)
        {
            if (!IsTokenAlive(token))
            {
                videoPlayer.loopPointReached -= OnEnd;
                yield break;
            }
            yield return null;
        }

        videoPlayer.loopPointReached -= OnEnd;

        // 終わったら消す
        Hide();
    }

    private void SetupClipSpeed(VideoClip clip, float targetDuration)
    {
        targetDuration = Mathf.Max(0.05f, targetDuration);

        videoPlayer.Stop();
        videoPlayer.clip = clip;

        float clipLen = Mathf.Max(0.001f, (float)clip.length);
        float speed = Mathf.Clamp(clipLen / targetDuration, 0.05f, 8f);
        videoPlayer.playbackSpeed = speed;
    }

    private IEnumerator WaitPrepared(int token, float timeoutSeconds)
    {
        float t = 0f;
        while (!videoPlayer.isPrepared && t < timeoutSeconds)
        {
            if (!IsTokenAlive(token)) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!videoPlayer.isPrepared)
            Debug.LogWarning("[VideoFadeOverlay] VideoPlayer Prepare timeout. (will try play anyway)");
    }

    private IEnumerator WaitRealtimeCancelable(int token, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (!IsTokenAlive(token)) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool EnsureReady()
    {
        if (videoPlayer == null || transitionImage == null)
        {
            Debug.LogError("[VideoFadeOverlay] Missing refs (VideoPlayer / RawImage).");
            return false;
        }
        return true;
    }

    private bool IsTokenAlive(int token) => token == _playToken;

    private void Show()
    {
        if (transitionImage != null)
            transitionImage.gameObject.SetActive(true);

        SetInputBlock(blockInputWhileVisible);
    }

    private void Hide()
    {
        if (transitionImage != null)
            transitionImage.gameObject.SetActive(false);

        SetInputBlock(false);
    }

    private void SetInputBlock(bool on)
    {
        if (_cg == null) return;
        _cg.blocksRaycasts = on;
        _cg.interactable = on;
    }

    // =========================================================
    // Optional: Inspector/Runtime setters
    // =========================================================
    public void SetDefaultDurations(float outSec, float inSec)
    {
        defaultOutDuration = Mathf.Max(0.05f, outSec);
        defaultInDuration = Mathf.Max(0.05f, inSec);
    }

    public void SetDefaultHold(float holdSec)
    {
        defaultHoldSeconds = Mathf.Max(0f, holdSec);
    }

    public void SetClips(VideoClip outClip, VideoClip inClip)
    {
        fadeOutClip = outClip;
        fadeInClip = inClip;
    }
}
