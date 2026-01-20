using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
public class NetworkStartUI : MonoBehaviour
{
    // ===== Singleton =====
    public static NetworkStartUI Instance { get; private set; }

    public enum StartMode
    {
        Host,
        Client
    }

    [Header("Start Settings")]
    [SerializeField] private StartMode startMode = StartMode.Host;

    [Tooltip("このシーン名になったときに InGame を監視する")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("GameManager.InGame == true になったら何秒後に接続するか")]
    [SerializeField, Min(0f)] private float autoStartDelay = 3f;

    [Tooltip("GameSceneでInGameになったら自動的に接続する")]
    [SerializeField] private bool autoStartOnInGame = true;

    [Header("Debug (Optional)")]
    [SerializeField] private bool allowManualKeyStart = true;

    [Header("UI References")]
    [SerializeField] private GameObject hostToggle;
    [SerializeField] private GameObject clientToggle;
    

    // 自動取得する参照
    private LanHost _lanHost;
    private LanClient _lanClient;

    // 1回だけ自動接続するガード
    private bool _autoStarted = false;
    private Coroutine _autoRoutine;

    

    // =========================
    // Unity Events
    // =========================
    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // シーンロード時に参照を拾い直す
        SceneManager.sceneLoaded += OnSceneLoaded;

        CacheLanComponents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheLanComponents();

        // 新しいシーンに入ったので「自動開始フラグ」をリセットしてもいい
        // もし「1回だけ」じゃなく「毎回開始」したい場合は true
        _autoStarted = false;

        // 前の自動Routineが残ってたら止める
        if (_autoRoutine != null)
        {
            StopCoroutine(_autoRoutine);
            _autoRoutine = null;
        }
    }

    // =========================
    // UI Bind (Toggle -> Enum)
    // =========================
    //private void BindUI()
    //{
    //    if (hostToggle != null)
    //    {
    //        hostToggle.onValueChanged.AddListener(isOn =>
    //        {
    //            if (isOn) startMode = StartMode.Host;
    //        });
    //    }

    //    if (clientToggle != null)
    //    {
    //        clientToggle.onValueChanged.AddListener(isOn =>
    //        {
    //            if (isOn) startMode = StartMode.Client;
    //        });
    //    }

    //}

    private void ApplyModeToUI(StartMode mode)
    {
        // UIの初期状態を整える
        if (hostToggle == null) return;
        if (clientToggle == null) return;

        if (hostToggle.activeSelf == true) startMode = StartMode.Host;
        if (clientToggle.activeSelf == true) startMode = StartMode.Client;
    }

    private void Update()
    {

        //BindUI();
        ApplyModeToUI(startMode);

        if (NetworkManager.Singleton == null) return;

        // ===== 手動キー開始（必要なら残す）=====
        if (allowManualKeyStart && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                startMode = StartMode.Host;
                TryStartNetwork();
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                startMode = StartMode.Client;
                TryStartNetwork();
            }
        }

        // ===== 自動開始（GameScene & InGame 監視）=====
        if (!autoStartOnInGame) return;
        if (_autoStarted) return;

        var active = SceneManager.GetActiveScene();
        if (!active.IsValid()) return;

        // Scene名が一致しているか？
        if (!string.Equals(active.name, gameSceneName)) return;

        // GameManager を探す
        var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null) return;

        // InGameがtrueになったら、3秒後に開始
        if (gm.InGame)
        {
            _autoStarted = true;
            _autoRoutine = StartCoroutine(AutoStartRoutine());
        }
    }
    // =========================
    // Core Methods
    // =========================
    private IEnumerator AutoStartRoutine()
    {
        // 3秒待つ
        yield return new WaitForSeconds(autoStartDelay);

        // 既に開始されていたら中止
        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) yield break;

        TryStartNetwork();
        _autoRoutine = null;
    }

    private void TryStartNetwork()
    {
        CacheLanComponents();

        if (startMode == StartMode.Host)
        {
            if (_lanHost == null)
            {
                Debug.LogError("[NetworkStartUI] LanHost が見つかりません。シーン内に LanHost を持つオブジェクトを置いてね。");
                return;
            }
            _lanHost.StartHostConnect();
            GameManager.Instance._buttonY.SetActive(true);

        }
        else // Client
        {
            if (_lanClient == null)
            {
                Debug.LogError("[NetworkStartUI] LanClient が見つかりません。シーン内に LanClient を持つオブジェクトを置いてね。");
                return;
            }
            _lanClient.StartClientConnect();
        }
    }

    private void CacheLanComponents()
    {
        // シーン内から自動取得（非アクティブも含める）
        _lanHost = FindAnyObjectByType<LanHost>(FindObjectsInactive.Include);
        _lanClient = FindAnyObjectByType<LanClient>(FindObjectsInactive.Include);

        // 見つかったかログを出したいならこれもアリ
        // Debug.Log($"[NetworkStartUI] Cache => LanHost: {(_lanHost ? "OK" : "None")} / LanClient: {(_lanClient ? "OK" : "None")}");
    }
}
