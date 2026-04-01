using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoFadeOverlay : MonoBehaviour
{
    public static VideoFadeOverlay Instance { get; private set; }

    [System.Serializable]
    public class FadeVideoSet
    {
        public string name;
        public VideoClip fadeOut;
        public VideoClip fadeIn;
    }

    [Header("Refs")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage transitionImage;

    [Header("Video Sets (Out/In pairs)")]
    [SerializeField] private List<FadeVideoSet> videoSets = new();

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
    // Public API
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

    // šIndexŽw’è”Å
    public void PlayFadeOutOnly(float? outDuration = null, int videoIndex = 0)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeOutOnly(token, outDuration ?? defaultOutDuration, videoIndex));
    }

    // šIndexŽw’è”Å
    public void PlayFadeInOnly(float? inDuration = null, int videoIndex = 0)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeInOnly(token, inDuration ?? defaultInDuration, videoIndex));
    }

    public void PlayFadeSequence(float? outDuration = null, float? holdSeconds = null, float? inDuration = null, int videoIndex = 0)
    {
        int token = ++_playToken;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFadeSequence(token,
            outDuration ?? defaultOutDuration,
            holdSeconds ?? defaultHoldSeconds,
            inDuration ?? defaultInDuration,
            videoIndex));
    }

    public IEnumerator CoFadeOutAndHold(float outDuration, float holdSeconds, int videoIndex = 0)
    {
        int token = ++_playToken;
        IsBusy = true;

        Show();
        var outClip = GetFadeOutClip(videoIndex);
        yield return PlayClipHoldLastFrame(token, outClip, outDuration);

        if (!IsTokenAlive(token)) yield break;

        if (holdSeconds > 0f)
            yield return WaitRealtimeCancelable(token, holdSeconds);
    }

    public IEnumerator CoFadeInAndHide(float inDuration, int videoIndex = 0)
    {
        int token = ++_playToken;
        IsBusy = true;

        Show();
        var inClip = GetFadeInClip(videoIndex);
        yield return PlayClipAndHide(token, inClip, inDuration);

        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    // =========================================================
    // Core Routines
    // =========================================================
    private IEnumerator CoFadeOutOnly(int token, float outDuration, int videoIndex)
    {
        IsBusy = true;
        Show();

        var clip = GetFadeOutClip(videoIndex);
        yield return PlayClipHoldLastFrame(token, clip, outDuration);

        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    private IEnumerator CoFadeInOnly(int token, float inDuration, int videoIndex)
    {
        IsBusy = true;
        Show();

        var clip = GetFadeInClip(videoIndex);
        yield return PlayClipAndHide(token, clip, inDuration);

        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    private IEnumerator CoFadeSequence(int token, float outDuration, float holdSeconds, float inDuration, int videoIndex)
    {
        IsBusy = true;
        Show();

        var outClip = GetFadeOutClip(videoIndex);
        var inClip = GetFadeInClip(videoIndex);

        yield return PlayClipHoldLastFrame(token, outClip, outDuration);
        if (!IsTokenAlive(token)) yield break;

        if (holdSeconds > 0f)
            yield return WaitRealtimeCancelable(token, holdSeconds);
        if (!IsTokenAlive(token)) yield break;

        yield return PlayClipAndHide(token, inClip, inDuration);
        if (!IsTokenAlive(token)) yield break;

        IsBusy = false;
    }

    // =========================================================
    // Clip Picking
    // =========================================================
    private VideoClip GetFadeOutClip(int index)
    {
        if (videoSets == null || videoSets.Count == 0)
        {
            Debug.LogWarning("[VideoFadeOverlay] videoSets is empty.");
            return null;
        }

        index = Mathf.Clamp(index, 0, videoSets.Count - 1);

        var clip = videoSets[index].fadeOut;
        if (clip == null)
            Debug.LogWarning($"[VideoFadeOverlay] FadeOut clip is null. index={index} name={videoSets[index].name}");

        return clip;
    }

    private VideoClip GetFadeInClip(int index)
    {
        if (videoSets == null || videoSets.Count == 0)
        {
            Debug.LogWarning("[VideoFadeOverlay] videoSets is empty.");
            return null;
        }

        index = Mathf.Clamp(index, 0, videoSets.Count - 1);

        var clip = videoSets[index].fadeIn;
        if (clip == null)
            Debug.LogWarning($"[VideoFadeOverlay] FadeIn clip is null. index={index} name={videoSets[index].name}");

        return clip;
    }

    // =========================================================
    // Clip Play Helpers
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

        videoPlayer.Pause();

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
    // Optional setters
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
}
