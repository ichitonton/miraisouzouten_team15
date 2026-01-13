using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class FadeManager : MonoBehaviour
{

    public static FadeManager Instance {  get; private set; }

    public enum FadeScope
    {
        LocalOnly,   // この端末だけ
        AllClients   // 接続中の全員（ホストが合図配信）
    }

    [Header("Refs")]
    [SerializeField] private FadeOverlay fade;            // FadeCanvas側
    [SerializeField] private NetworkFadeMessenger netFade;    // FadeSystem側（NamedMessageで合図）

    [Header("Durations")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;

    [Header("FadeOnly")]
    [SerializeField, Min(0f)] private float holdBlackSeconds = 0.0f;

    [Header("Behavior")]
    [Tooltip("シーン遷移後に自動でフェードインする（ロード完了を待ってから実行）")]
    [SerializeField] private bool autoFadeInAfterSceneLoad = true;

    bool _running;

    bool HasNet => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    bool IsServer => HasNet && NetworkManager.Singleton.IsServer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 参照未設定なら拾う（Unity6の新API）
        if (fade == null) fade = Object.FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);
        if (netFade == null) netFade = Object.FindFirstObjectByType<NetworkFadeMessenger>(FindObjectsInactive.Include);
    }

    // =========================
    // 演出だけ（シーン跨がない）
    // =========================
    public void PlayFadeOnly(FadeScope scope)
    {
        if (_running) return;
        StartCoroutine(FadeOnlyRoutine(scope));
    }

    IEnumerator FadeOnlyRoutine(FadeScope scope)
    {
        _running = true;

        yield return FadeOutRoutine(scope);

        if (holdBlackSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdBlackSeconds);

        yield return FadeInRoutine(scope);

        _running = false;
    }

    // =========================
    // フェード + シーン遷移（string）
    // =========================
    public void PlayToScene(string sceneName, FadeScope scope)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[FadeDirector] sceneName が空です");
            return;
        }
        if (_running) return;
        StartCoroutine(TransitionRoutine(sceneName, scope));
    }

    // =========================
    // フェード + シーン遷移（SceneId）
    // =========================
    public void PlayToScene(NetworkSceneManager.SceneId sceneId, FadeScope scope)
    {
        var nsm = NetworkSceneManager.Instance;
        if (nsm == null)
        {
            Debug.LogError("[FadeDirector] NetworkSceneManager.Instance が見つかりません");
            return;
        }

        if (!nsm.TryResolveSceneName(sceneId, out var sceneName))
        {
            Debug.LogError($"[FadeDirector] SceneId {sceneId} のシーン名解決に失敗");
            return;
        }

        PlayToScene(sceneName, scope);
    }

    IEnumerator TransitionRoutine(string sceneName, FadeScope scope)
    {
        _running = true;

        // 1) FadeOut
        yield return FadeOutRoutine(scope);

        // 2) シーン遷移
        if (scope == FadeScope.LocalOnly)
        {
            // ★ LocalOnly：普通のローカル遷移でOK（NetworkSceneManagerは使わない）
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                
                // 自分だけ別シーンへ行くなら、先にオフライン化して同期破壊を避ける
                NetworkManager.Singleton.Shutdown();
                
            }

            Debug.Log("[FadeDirector] Scene loaded, starting FadeIn");

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (autoFadeInAfterSceneLoad)
                yield return FadeInRoutine(scope);

            _running = false;
            yield break;
        }

        // ★ AllClients：ネットワーク同期遷移（君の NetworkSceneManager を使う）
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[FadeDirector] AllClients のシーン遷移は Host/Server で実行してください");
                _running = false;
                yield break;
            }
        }

        var nsm = NetworkSceneManager.Instance;
        if (nsm == null)
        {
            Debug.LogError("[FadeDirector] NetworkSceneManager.Instance が見つかりません");
            _running = false;
            yield break;
        }

        // NGO or ローカル（nm==null）どっちでも、君の NetworkSceneManager が最終的にやってくれる
        // ただし「完了待ち」は NGO のイベントを使うのが確実
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            bool loadDone = false;
            var ngoSceneMgr = NetworkManager.Singleton.SceneManager;

            void OnLoadEventCompleted(string loadedSceneName, LoadSceneMode mode,
                System.Collections.Generic.List<ulong> completedClients,
                System.Collections.Generic.List<ulong> timedOutClients)
            {
                if (loadedSceneName == sceneName) loadDone = true;
            }

            ngoSceneMgr.OnLoadEventCompleted += OnLoadEventCompleted;

            nsm.LoadSceneRequest(sceneName);

            while (!loadDone) yield return null;

            ngoSceneMgr.OnLoadEventCompleted -= OnLoadEventCompleted;
        }
        else
        {
            // オフラインでも「AllClients指定」はあり得るので、ここは普通にローカルロード完了待ち
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        // 3) FadeIn
        if (autoFadeInAfterSceneLoad)
            yield return FadeInRoutine(scope);

        _running = false;
    }

    // =========================
    // 単体フェードAPI（他の場所からも使える）
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

    IEnumerator FadeOutRoutine(FadeScope scope)
    {
        StartFadeOut(scope, fadeOutDuration);
        yield return new WaitForSecondsRealtime(fadeOutDuration);
    }

    IEnumerator FadeInRoutine(FadeScope scope)
    {
        StartFadeIn(scope, fadeInDuration);
        yield return new WaitForSecondsRealtime(fadeInDuration);
    }

    void StartFadeOut(FadeScope scope, float duration)
    {
        var f = ResolveFade();
        if (f == null)
        {
            Debug.LogError("[FadeDirector] FadeOverlay が見つかりません（FadeCanvasが存在する？）");
            return;
        }

        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            f.FadeOut(duration);
            return;
        }

        if (IsServer) netFade?.SendFadeOutToAll(duration);
        else f.FadeOut(duration);
    }

    void StartFadeIn(FadeScope scope, float duration)
    {
        var f = ResolveFade();
        if (f == null)
        {
            Debug.LogError("[FadeDirector] FadeOverlay が見つかりません（FadeCanvasが存在する？）");
            return;
        }

        if (!HasNet || scope == FadeScope.LocalOnly)
        {
            f.FadeIn(duration);
            return;
        }

        if (IsServer) netFade?.SendFadeInToAll(duration);
        else f.FadeIn(duration); // クライアントはローカルに落とす
    }

    private FadeOverlay ResolveFade()
    {
        // 破棄済み参照は Unity の == null 判定で true になる
        if (fade == null) fade = FadeOverlay.Instance;

        if (fade == null)
            fade = Object.FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        return fade;
    }

}
