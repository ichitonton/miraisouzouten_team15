using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    public enum FadeScope
    {
        LocalOnly,   // この端末だけ
        AllClients   // 接続中の全員（Hostが合図配信）
    }

    [Header("Refs (optional)")]
    [SerializeField] private FadeOverlay fade;                  // FadeCanvas側
    [SerializeField] private NetworkFadeMessenger netFade;      // FadeSystem側（合図配信用）

    [Header("Durations")]
    [SerializeField, Min(0f)] public float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] public float fadeInDuration = 0.35f;

    [Header("FadeOnly")]
    [SerializeField, Min(0f)] private float holdBlackSeconds = 0.0f;

    [Header("Behavior")]
    [Tooltip("シーン遷移後に自動でフェードインする（ロード完了を待ってから実行）")]
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

        // 初回参照解決
        ResolveFade();
        ResolveNetFade();
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
        StartCoroutine(FadeOnlyRoutine(scope));
    }

    // =========================
    // Public API：フェード + シーン遷移（string）
    // =========================
    public void PlayToScene(string sceneName, FadeScope scope)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[FadeManager] sceneName が空です");
            return;
        }
        if (_running) return;

        StartCoroutine(TransitionRoutine(sceneName, scope));
    }

    // =========================
    // Public API：フェード + シーン遷移（SceneId）
    // =========================
    public void PlayToScene(NetworkSceneManager.SceneId sceneId, FadeScope scope)
    {
        var nsm = NetworkSceneManager.Instance;
        if (nsm == null)
        {
            Debug.LogError("[FadeManager] NetworkSceneManager.Instance が見つかりません");
            return;
        }

        if (!nsm.TryResolveSceneName(sceneId, out var sceneName))
        {
            Debug.LogError($"[FadeManager] SceneId {sceneId} のシーン名解決に失敗");
            return;
        }

        PlayToScene(sceneName, scope);
    }

    // =========================
    // Public API：単体フェード（他からも使える）
    // =========================
    public void PlayFadeOut(FadeScope scope)
    {
        if (_running) return;
        StartCoroutine(FadeOutRoutine(scope));
    }

    public void PlayFadeIn(FadeScope scope)
    {
        if (_running) return;
        StartCoroutine(FadeInRoutine(scope));
    }

    // =========================
    // Routine：FadeOnly
    // =========================
    private IEnumerator FadeOnlyRoutine(FadeScope scope)
    {
        _running = true;

        yield return FadeOutRoutine(scope);

        if (holdBlackSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdBlackSeconds);

        yield return FadeInRoutine(scope);

        _running = false;
    }

    // =========================
    // Routine：Transition
    // =========================
    private IEnumerator TransitionRoutine(string sceneName, FadeScope scope)
    {
        _running = true;

        // 1) FadeOut
        yield return FadeOutRoutine(scope);

        // 2) Scene Load
        if (scope == FadeScope.LocalOnly)
        {
            // ネット繋がってたら自分だけ移動する前にオフライン化（同期崩壊防止）
            if (shutdownNetworkBeforeLocalLoad && HasNet)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null; // 1フレ待つと安定
            }

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            // シーン切替後は参照が死んでる可能性があるので必ず更新
            ResolveFade();
            ResolveNetFade();

            if (autoFadeInAfterSceneLoad)
                yield return FadeInRoutine(scope);

            _running = false;
            yield break;
        }

        // --- AllClients ---
        if (HasNet)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                // ClientがAllClientsを叩いたら失敗が正しい（Hostに依頼する設計なら別途）
                Debug.LogWarning("[FadeManager] AllClients のシーン遷移は Host/Server で実行してください");
                _running = false;
                yield break;
            }

            // NGOのSceneManagerで同期ロード
            bool loadDone = false;
            var ngoSceneMgr = NetworkManager.Singleton.SceneManager;

            void OnLoadEventCompleted(string loadedSceneName, LoadSceneMode mode,
                List<ulong> completedClients, List<ulong> timedOutClients)
            {
                if (loadedSceneName == sceneName)
                    loadDone = true;
            }

            ngoSceneMgr.OnLoadEventCompleted += OnLoadEventCompleted;

            // 君の NetworkSceneManager 経由でもOK（中で nm.SceneManager.LoadScene 呼んでる）
            // ただしここは「確実性重視」で直接NGOを呼ぶのが最強
            ngoSceneMgr.LoadScene(sceneName, LoadSceneMode.Single);

            float t = 0f;
            while (!loadDone && t < allClientsLoadTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            ngoSceneMgr.OnLoadEventCompleted -= OnLoadEventCompleted;

            if (!loadDone)
            {
                Debug.LogWarning($"[FadeManager] AllClients LoadScene timeout: {sceneName} ({allClientsLoadTimeout}s). FadeInへ進みます。");
            }
        }
        else
        {
            // ネット無しでAllClients指定された場合はローカルロードにフォールバック
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        // 3) FadeIn
        // シーン切替後は参照が死んでる可能性があるので必ず更新
        ResolveFade();
        ResolveNetFade();

        if (autoFadeInAfterSceneLoad)
            yield return FadeInRoutine(scope);

        _running = false;
    }

    // =========================
    // Routine：Fade Out/In
    // =========================
    private IEnumerator FadeOutRoutine(FadeScope scope)
    {
        StartFadeOut(scope, fadeOutDuration);
        yield return new WaitForSecondsRealtime(fadeOutDuration);
    }

    private IEnumerator FadeInRoutine(FadeScope scope)
    {
        StartFadeIn(scope, fadeInDuration);
        yield return new WaitForSecondsRealtime(fadeInDuration);
    }

    // =========================
    // Core：Start Fade
    // =========================
    private void StartFadeOut(FadeScope scope, float duration)
    {
        var f = ResolveFade();
        if (f == null)
        {
            Debug.LogError("[FadeManager] FadeOverlay が見つかりません（FadeCanvasが存在する？）");
            return;
        }

        // ローカル or ネット無し
        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            f.FadeOut(duration);
            return;
        }

        // AllClients：Hostは必ず自分もフェード
        f.FadeOut(duration);

        // 全員へ合図
        if (IsServer)
        {
            var n = ResolveNetFade();
            if (n != null) n.SendFadeOutToAll(duration);
            else Debug.LogWarning("[FadeManager] NetworkFadeMessenger が見つからず AllClients FadeOut を配信できません");
        }
    }

    private void StartFadeIn(FadeScope scope, float duration)
    {
        var f = ResolveFade();
        if (f == null)
        {
            Debug.LogError("[FadeManager] FadeOverlay が見つかりません（FadeCanvasが存在する？）");
            return;
        }

        // ローカル or ネット無し
        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            f.FadeIn(duration);
            return;
        }

        // AllClients：Hostは必ず自分もフェード
        f.FadeIn(duration);

        // 全員へ合図
        if (IsServer)
        {
            var n = ResolveNetFade();
            if (n != null) n.SendFadeInToAll(duration);
            else Debug.LogWarning("[FadeManager] NetworkFadeMessenger が見つからず AllClients FadeIn を配信できません");
        }
    }

    // =========================
    // Resolve：参照が死んでても復活させる
    // =========================
    private FadeOverlay ResolveFade()
    {
        // 破棄済み参照は Unity の == null 判定で true になる
        if (fade == null) fade = FadeOverlay.Instance;

        if (fade == null)
            fade = Object.FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        return fade;
    }

    private NetworkFadeMessenger ResolveNetFade()
    {
        if (netFade == null)
            netFade = Object.FindFirstObjectByType<NetworkFadeMessenger>(FindObjectsInactive.Include);

        return netFade;
    }
}
