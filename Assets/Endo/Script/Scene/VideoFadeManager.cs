using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VideoFadeManager : MonoBehaviour
{
    public static VideoFadeManager Instance { get; private set; }

    public enum FadeScope
    {
        LocalOnly,
        AllClients
    }

    [Header("Refs (optional)")]
    [SerializeField] private VideoFadeOverlay overlay;
    [SerializeField] private NetworkVideoFadeMessenger netVideo;

    [Header("Durations")]
    [SerializeField, Min(0.05f)] public float fadeOutDuration = 0.35f;
    [SerializeField, Min(0.05f)] public float fadeInDuration = 0.35f;

    [Header("Hold (Out -> wait -> In)")]
    [SerializeField, Min(0f)] public float holdSeconds = 0.2f;

    [Header("Video Index")]
    [Tooltip("Overlay側の videoSets から使うIndex")]
    public int _videoIndex = 0; // ★追加

    [Header("Behavior")]
    [Tooltip("シーン遷移後に自動でフェードインする")]
    [SerializeField] private bool autoFadeInAfterSceneLoad = true;

    [Tooltip("LocalOnly遷移時、ネットが繋がってたら先にShutdownする（同期崩壊防止）")]
    [SerializeField] private bool shutdownNetworkBeforeLocalLoad = true;

    [Tooltip("AllClientsのロード完了待ちタイムアウト（秒）")]
    [SerializeField, Min(1f)] private float allClientsLoadTimeout = 15f;

    private bool _running = false;

    private bool HasNet => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsServer => HasNet && NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolveOverlay();
        ResolveNet();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =========================
    // Public API：演出だけ
    // =========================
    public void PlayFadeOnly(FadeScope scope)
    {
        if (_running) return;
        StartCoroutine(FadeOnlyRoutine(scope, _videoIndex));
    }

    // ★Index指定もできる版（便利）
    public void PlayFadeOnly(FadeScope scope, int videoIndex)
    {
        if (_running) return;
        StartCoroutine(FadeOnlyRoutine(scope, videoIndex));
    }

    // =========================
    // Public API：演出 + シーン遷移
    // =========================
    public void PlayToScene(string sceneName, FadeScope scope)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[VideoFadeManager] sceneName が空です");
            return;
        }
        if (_running) return;

        StartCoroutine(TransitionRoutine(sceneName, scope, _videoIndex));
    }

    // ★Index指定もできる版（便利）
    public void PlayToScene(string sceneName, FadeScope scope, int videoIndex)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[VideoFadeManager] sceneName が空です");
            return;
        }
        if (_running) return;

        StartCoroutine(TransitionRoutine(sceneName, scope, videoIndex));
    }

    // =========================
    // Routine：FadeOnly
    // =========================
    private IEnumerator FadeOnlyRoutine(FadeScope scope, int videoIndex)
    {
        _running = true;

        yield return FadeOutRoutine(scope, videoIndex);

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        yield return FadeInRoutine(scope, videoIndex);

        _running = false;
    }

    // =========================
    // Routine：Transition
    // =========================
    private IEnumerator TransitionRoutine(string sceneName, FadeScope scope, int videoIndex)
    {
        _running = true;

        // 1) FadeOut（動画：最終フレーム保持）
        yield return FadeOutRoutine(scope, videoIndex);

        // 2) Hold
        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        // 3) Scene Load
        if (scope == FadeScope.LocalOnly)
        {
            if (shutdownNetworkBeforeLocalLoad && HasNet)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
            }

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            ResolveOverlay();
            ResolveNet();

            if (autoFadeInAfterSceneLoad)
                yield return FadeInRoutine(scope, videoIndex);

            _running = false;
            yield break;
        }

        // --- AllClients ---
        if (HasNet)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[VideoFadeManager] AllClients のシーン遷移は Host/Server で実行してください");
                _running = false;
                yield break;
            }

            bool loadDone = false;
            var ngoSceneMgr = NetworkManager.Singleton.SceneManager;

            void OnLoadEventCompleted(string loadedSceneName, LoadSceneMode mode,
                List<ulong> completedClients, List<ulong> timedOutClients)
            {
                if (loadedSceneName == sceneName)
                    loadDone = true;
            }

            ngoSceneMgr.OnLoadEventCompleted += OnLoadEventCompleted;
            ngoSceneMgr.LoadScene(sceneName, LoadSceneMode.Single);

            float t = 0f;
            while (!loadDone && t < allClientsLoadTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            ngoSceneMgr.OnLoadEventCompleted -= OnLoadEventCompleted;

            if (!loadDone)
                Debug.LogWarning($"[VideoFadeManager] AllClients LoadScene timeout: {sceneName} ({allClientsLoadTimeout}s). FadeInへ進みます。");
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        // 4) FadeIn
        ResolveOverlay();
        ResolveNet();

        if (autoFadeInAfterSceneLoad)
            yield return FadeInRoutine(scope, videoIndex);

        _running = false;
    }

    // =========================
    // Fade Out/In Routine
    // =========================
    private IEnumerator FadeOutRoutine(FadeScope scope, int videoIndex)
    {
        StartFadeOut(scope, fadeOutDuration, videoIndex);
        yield return new WaitForSecondsRealtime(fadeOutDuration);
    }

    private IEnumerator FadeInRoutine(FadeScope scope, int videoIndex)
    {
        StartFadeIn(scope, fadeInDuration, videoIndex);
        yield return new WaitForSecondsRealtime(fadeInDuration);
    }

    // =========================
    // Core：Start Fade（ローカル/全員）
    // =========================
    private void StartFadeOut(FadeScope scope, float duration, int videoIndex)
    {
        var ov = ResolveOverlay();
        if (ov == null)
        {
            Debug.LogError("[VideoFadeManager] VideoFadeOverlay が見つかりません");
            return;
        }

        // ローカル or ネット無し
        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            ov.PlayFadeOutOnly(duration, videoIndex);
            return;
        }

        // AllClients：Hostは自分も再生
        ov.PlayFadeOutOnly(duration, videoIndex);

        if (IsServer)
        {
            var n = ResolveNet();
            if (n != null) n.SendFadeOutToAll(duration, videoIndex);
            else Debug.LogWarning("[VideoFadeManager] NetworkVideoFadeMessenger が見つからず AllClients FadeOut を配信できません");
        }
    }

    private void StartFadeIn(FadeScope scope, float duration, int videoIndex)
    {
        var ov = ResolveOverlay();
        if (ov == null)
        {
            Debug.LogError("[VideoFadeManager] VideoFadeOverlay が見つかりません");
            return;
        }

        // ローカル or ネット無し
        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            ov.PlayFadeInOnly(duration, videoIndex);
            return;
        }

        // AllClients：Hostは自分も再生
        ov.PlayFadeInOnly(duration, videoIndex);

        if (IsServer)
        {
            var n = ResolveNet();
            if (n != null) n.SendFadeInToAll(duration, videoIndex);
            else Debug.LogWarning("[VideoFadeManager] NetworkVideoFadeMessenger が見つからず AllClients FadeIn を配信できません");
        }
    }

    // =========================
    // Resolve：参照が死んでても復活させる
    // =========================
    private VideoFadeOverlay ResolveOverlay()
    {
        if (overlay == null) overlay = VideoFadeOverlay.Instance;
        if (overlay == null)
            overlay = Object.FindFirstObjectByType<VideoFadeOverlay>(FindObjectsInactive.Include);
        return overlay;
    }

    private NetworkVideoFadeMessenger ResolveNet()
    {
        if (netVideo == null)
            netVideo = Object.FindFirstObjectByType<NetworkVideoFadeMessenger>(FindObjectsInactive.Include);
        return netVideo;
    }
}
