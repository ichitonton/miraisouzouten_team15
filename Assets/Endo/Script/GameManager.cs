#if UNITY_EDITOR
using NUnit.Framework;
using NUnit.Framework.Interfaces;
#endif

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine.InputSystem;
using Unity.VisualScripting;
using UnityEditor.Experimental;
using UnityEngine.Video;

public class GameManager : NetworkBehaviour
{
    [Header("ローカル内で動くやつだからNetworkObjectついてないプレイヤー入れてね")]
    [SerializeField] private GameObject _player1;
    [SerializeField] private GameObject _player2;

    [Header("SpawnPoint")]
    [SerializeField] private Transform _spawnPos;

    [Header("StartPosition (slot順に使う)")]
    [SerializeField] private Transform[] _pivot;

    // （残すだけ：UI/Pad制御は LocalPadSession 側へ）
    [Header("Connection UI (moved to LocalPadSession)")]
    [SerializeField] private GameObject blurUI;
    [SerializeField] private GameObject connectUI;
    [SerializeField] private GameObject gamepadUI1;
    [SerializeField] private GameObject gamepadUI2;

    [Header("Gameplay UI (moved to LocalPadSession)")]
    [SerializeField] private List<GameObject> gameplayUIs = new List<GameObject>();

    [Header("Start Condition (moved to LocalPadSession)")]
    [SerializeField] private int requiredGamepads = 2;
    [SerializeField] private bool useDpadDownToStart = true;

    [Header("State")]
    public bool InGame { get; private set; } = false;

    // ローカル生成プレイヤー（オフライン専用）
    private GameObject _p1;
    private GameObject _p2;

    private GameObject[] _players;
    private GameObject _networkUi;

    public bool _IsLanModeActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public static GameManager Instance { get; private set; }

    public enum Mode { Keyboard, Gamepad }
    public enum OnlineMode { OnePC, MoreTowPC }

    [Header("操作モード設定")]
    public Mode _controlMode = Mode.Keyboard;

    [Header("通信モード")]
    public OnlineMode _onlineMode = OnlineMode.OnePC;

    public List<GameObject> _objectList = new List<GameObject>();

    //  ネットワークオブジェクト参照（破棄済み参照が混ざっても落ちないように使う）
    public NetworkList<NetworkObjectReference> _networkObjectList = new NetworkList<NetworkObjectReference>();

    private bool _isStart = false;

    //  何回もStartが走らないように保険
    private bool _startingRoutine = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Application.targetFrameRate = 60;
    }

    public override void OnNetworkDespawn()
    {
        //  サーバーだけが破棄責任を持つ
        if (!IsServer) return;

        Debug.Log("[GameManager] OnNetworkDespawn (Server cleanup)");

        //  NetworkList は破棄タイミング次第で触ると危険なことがあるので try で守る
        try
        {
            List<NetworkObject> toDespawn = new List<NetworkObject>();

            foreach (var r in _networkObjectList)
            {
                if (!r.TryGet(out var no)) continue;
                if (no == null) continue;

                // Player/Ropeなども混ざる前提
                if (no.IsSpawned)
                    toDespawn.Add(no);
            }

            foreach (var no in toDespawn)
            {
                //  Despawn(true) で destroy までやってくれるので Destroy() は不要
                no.Despawn(true);
            }

            _networkObjectList.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameManager] OnNetworkDespawn cleanup skipped (safe): {e.Message}");
        }
    }

    // ==========================================================
    // ★LocalPadSession から呼ぶ入口
    // ==========================================================
    public void StartLocalGameRequest()
    {
        // LAN中：ローカルプレイヤーは作らない（絶対）
        if (_IsLanModeActive)
        {
            // ホストだけが開始演出＆ネットワーク開始を指揮
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                StartGameAsHostNetwork();
            }
            else
            {
                Debug.Log("[GameManager] Clientから開始要求。ホスト主導運用なら何もしない。");
                // 「クライアントから開始依頼したい」なら ServerRpc をここで実装する
            }
            return;
        }

        // オフライン：ここでだけローカルプレイヤー生成
        StartGameOfflineLocal();
    }

    // ==========================================================
    // ★LAN（ネットワーク）開始：ローカルプレイヤー生成はしない
    // ==========================================================
    private void StartGameAsHostNetwork()
    {
        if (_startingRoutine) return; // 二重開始防止
        _startingRoutine = true;

        //フェードの秒数設定
        if (VideoFadeManager.Instance != null)
        {
            VideoFadeManager.Instance.fadeInDuration = 1.1f;
            VideoFadeManager.Instance.fadeOutDuration = 1.1f;

            VideoFadeManager.Instance._videoIndex = 0;
            VideoFadeManager.Instance.PlayFadeOnly(VideoFadeManager.FadeScope.AllClients);
        }

        var players = GameObject.FindGameObjectsWithTag("Player");

        //プレイヤー操作不能（ここは君の実装を後で入れる）
        foreach (var p in players)
        {
            // 例：p.GetComponent<PlayerController>()?.SetEnabled(false);
        }

        //ゲームスタート
        StartCoroutine(StartGame());
    }

    private IEnumerator StartGame()
    {
        float time = (VideoFadeManager.Instance != null) ? VideoFadeManager.Instance.fadeOutDuration : 1.1f;
        yield return new WaitForSeconds(time);

        //ムービーを流す
        StartCoroutine(PlayMovie());

        // ここが昨日からの修正ポイント
        // StartCoroutine("TeleportPlayer", 0.3f); ← これは危険（voidには効かない＆止まりやすい）
        StartCoroutine(TeleportAfterDelay(0.3f));

        _isStart = true;
        InGame = true;

        Debug.Log("[GameManager] Network Start (Host) done. (No local player spawn)");
        _startingRoutine = false;
    }

    private IEnumerator TeleportAfterDelay(float sec)
    {
        yield return new WaitForSeconds(sec);

        // Hostのみがテレポートを指示する
        if (NetworkManager.Singleton == null) yield break;
        if (!NetworkManager.Singleton.IsServer) yield break;

        TeleportPlayersBySlotAndConnect();
    }

    // ==========================================================
    // ★オフライン開始：ローカルプレイヤーを生成するのはここだけ
    // ==========================================================
    private void StartGameOfflineLocal()
    {
        if (_player1 == null || _player2 == null)
        {
            Debug.LogError("[GameManager] _player1 / _player2 が設定されていません");
            return;
        }

        if (_spawnPos == null)
        {
            Debug.LogError("[GameManager] _spawnPos が設定されていません");
            return;
        }

        // 二重生成防止
        if (_p1 != null || _p2 != null)
        {
            Debug.LogWarning("[GameManager] Offline players already spawned.");
            return;
        }

        // ★Transform自体は動かさず、位置ベクトルだけずらす（元コードの副作用防止）
        Vector3 basePos = _spawnPos.position;
        Vector3 p1Pos = basePos + Vector3.right * 1f;
        Vector3 p2Pos = basePos + Vector3.left * 1f;
        Quaternion rot = _spawnPos.rotation;

        _p1 = Instantiate(_player1, p1Pos, rot);
        _p2 = Instantiate(_player2, p2Pos, rot);

        // ここで LocalPadSession に登録
        if (LocalPadSession.Instance != null)
        {
            LocalPadSession.Instance.RegisterPlayers(_p1, _p2);
        }

        _isStart = true;
        InGame = true;

        Debug.Log("[GameManager] Offline Start done. (Local players spawned)");
    }

    // ==========================================================
    // ムービー
    // ==========================================================
    private IEnumerator PlayMovie()
    {
        // movie が無い可能性もあるので安全に
        var movie = Object.FindFirstObjectByType<GameStartMovie>(FindObjectsInactive.Include);

        //全クライアントに流す
        PlayMovieClientRpc();

        if (movie == null || movie._videoPlayer == null)
        {
            Debug.LogWarning("[GameManager] GameStartMovie or VideoPlayer not found. Skip wait.");
            // 代わりに即フェード→UIへ
            if (VideoFadeManager.Instance != null)
            {
                //VideoFadeManager.Instance._videoIndex = 1;
                VideoFadeManager.Instance.PlayFadeOnly(VideoFadeManager.FadeScope.AllClients);
                
            }

            StartCoroutine(PlayStartUI());
            yield break;
        }

        var video = movie._videoPlayer;

        float v_time = (float)video.length;


        v_time -= (VideoFadeManager.Instance != null ? VideoFadeManager.Instance.fadeOutDuration : 0f);


        if (v_time < 0f) v_time = 0f;

        yield return new WaitForSeconds(v_time);

        if (VideoFadeManager.Instance != null)
        {
            //VideoFadeManager.Instance._videoIndex = 1;
            VideoFadeManager.Instance.PlayFadeOnly(VideoFadeManager.FadeScope.AllClients);
            
        }

        StartCoroutine(PlayStartUI());
    }

    private IEnumerator PlayStartUI()
    {
        float wait = 0f;
        if (VideoFadeManager.Instance != null)
            wait = VideoFadeManager.Instance.fadeInDuration + VideoFadeManager.Instance.fadeOutDuration;

        yield return new WaitForSeconds(wait);

        //スタートUI（Hostが呼べば完全一致版が全員同期開始する）
        if (GameStartUIManager.Instance != null)
            GameStartUIManager.Instance.Play();

        //イベントのスタート
        StartCoroutine(StartEvent());
    }

    [ClientRpc]
    void PlayMovieClientRpc()
    {
        var movie = Object.FindFirstObjectByType<GameStartMovie>(FindObjectsInactive.Include);

        if (movie == null)
        {
            Debug.LogWarning("[GameManager] GameStartMovie が見つからない（ClientRpc）");
            return;
        }

        movie.gameObject.SetActive(true);
        movie.Play();
    }

    // ==========================================================
    // Teleport（修正版）
    // 接続中の ClientId を列挙して、pivot は slot（参加順）で割り当てる
    // ==========================================================
    private void TeleportPlayersBySlotAndConnect()
    {
        if (!_IsLanModeActive) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        if (_pivot == null || _pivot.Length == 0)
        {
            Debug.LogError("[GameManager] _pivot が設定されていません");
            return;
        }

        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        int slot = 0;
        foreach (var clientId in ids)
        {
            if (slot >= _pivot.Length) break;
            if (_pivot[slot] == null) { slot++; continue; }

            // pivot は slot、対象判定は clientId
            PlayerTeleportAndConnect(_pivot[slot].position, clientId);

            slot++;
        }

        if (NetworkSoundManager.Instance != null)
            NetworkSoundManager.Instance.PlayBgm("FuwaFuwa", NetworkSoundManager.SoundScope.AllClients, true);

        _isStart = true;
    }

    //タイマー開始
	public void OnCountdownStartSpriteShown()
	{
		if (NetworkManager.Singleton == null) return;
		if (!NetworkManager.Singleton.IsServer) return; // ホストだけが開始指示

		var timer = Object.FindFirstObjectByType<TimerManager>();
		if (timer != null)
			timer.StartTimerServerRpc();
	}


	private IEnumerator StartEvent()
    {
        // LANじゃなければやらない
        if (!_IsLanModeActive) yield break;

        yield return new WaitForSeconds(30f);

        //イベントを始めます
        if (GameEventManager.Instance != null)
            GameEventManager.Instance.TryStartAutoLoop();
    }

    private void OnGUI()
    {
        // 既存デバッグGUIは残す（LAN中ホストのみ）
        if (!InGame) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 100, 120, 30), "ゲームスタート"))
        {
            StartLocalGameRequest();
        }
    }

    // ==========================================================
    //  Teleport + RopeConnect（安全化）
    // id = 本物の clientId を入れること！
    // ==========================================================
    private void PlayerTeleportAndConnect(Vector3 pos, ulong id)
    {
        //  NetworkListが壊れてても落ちない
        List<GameObject> players = new List<GameObject>();

        try
        {
            foreach (var playerRef in _networkObjectList)
            {
                if (!playerRef.TryGet(out var playerObj)) continue;
                if (playerObj == null) continue;

                if (playerObj.gameObject.CompareTag("Player") && playerObj.OwnerClientId == id)
                {
                    players.Add(playerObj.gameObject);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameManager] PlayerTeleportAndConnect: list access skipped (safe): {e.Message}");
            return;
        }

        if (players.Count <= 0) return;

        //  Teleport（複数ある時はZずらし）
        for (int i = 0; i < players.Count; i++)
        {
            var go = players[i];
            if (go == null) continue;

            Vector3 newPos = pos;
            newPos.z = newPos.z + (i * -1f);

            var netTrans = go.GetComponent<NetworkTransform>();
            if (netTrans == null) continue;

            //  Server側でTeleportする想定
            netTrans.Teleport(newPos, go.transform.rotation, go.transform.localScale);
        }

        //  1人しかいないならロープ不要
        if (players.Count <= 1) return;

        //  Rope探してConnect（OwnerClientId一致）
        try
        {
            foreach (var ropeRef in _networkObjectList)
            {
                if (!ropeRef.TryGet(out var ropeObj)) continue;
                if (ropeObj == null) continue;

                if (ropeObj.gameObject.CompareTag("Rope") && ropeObj.OwnerClientId == id)
                {
                    var joint = ropeObj.GetComponent<PlayerJoint>();
                    if (joint != null && joint.isActiveAndEnabled)
                        StartCoroutine(joint.DelayConnect());

                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GameManager] Rope connect skipped (safe): {e.Message}");
        }
    }
}
