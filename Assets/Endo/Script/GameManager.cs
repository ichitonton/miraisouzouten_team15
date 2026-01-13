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

public class GameManager : NetworkBehaviour
{
    [Header("ローカル内で動くやつだからNetworkObjectついてないプレイヤー入れてね")]
    [SerializeField] private GameObject _player1;
    [SerializeField] private GameObject _player2;

    [Header("SpawnPoint")]
    [SerializeField] private Transform _spawnPos;
    [Header("StartPosition")]
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

    public NetworkList<NetworkObjectReference> _networkObjectList = new NetworkList<NetworkObjectReference>();

    private bool _isStart = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Application.targetFrameRate = 60;
    }

    // ==========================================================
    // ★LocalPadSession から呼ぶ入口
    // ==========================================================
    public void StartLocalGameRequest()
    {
        //if (_isStart) return;

        // LAN中：ローカルプレイヤーは作らない（絶対）
        if (_IsLanModeActive)
        {
            // ホストだけが開始演出＆ネットワーク開始を指揮
            if (NetworkManager.Singleton.IsServer)
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
        PlayMovieClientRpc();
        StartCoroutine("TeleportPlayer", 0.3f);

        //if (GameEventManager.Instance != null) GameEventManager.Instance.TryStartAutoLoop();

        _isStart = true;
        InGame = true;

        Debug.Log("[GameManager] Network Start (Host) done. (No local player spawn)");
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

    [ClientRpc]
    void PlayMovieClientRpc()
    {
        var movie = Object.FindFirstObjectByType<GameStartMovie>(FindObjectsInactive.Include);

        if (movie == null)
        {
            Debug.LogError("GameStartMovie が見つからない");
            return;
        }

        movie.gameObject.SetActive(true);
        movie.Play();
    }

    void TeleportPlayer()
    {
        if (!_IsLanModeActive) return;

        for (int i = 0; i < 3; i++)
        {
            if (_pivot == null || _pivot.Length <= i) continue;
            if (_pivot[i] == null) continue;

            PlayerTeleportAndConnect(_pivot[i].position, (ulong)i);

            if (NetworkSoundManager.Instance != null)
                NetworkSoundManager.Instance.PlayBgm("FuwaFuwa", NetworkSoundManager.SoundScope.AllClients, true);

            _isStart = true;
        }
    }

    private void OnGUI()
    {
        // 既存デバッグGUIは残す（LAN中ホストのみ）
        if (!InGame) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsServer) return;

        if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 100, 120, 30), "ゲームスタート"))
        {
            // LAN中：ホスト開始だけ実行（ローカル生成しない）
            StartLocalGameRequest();
        }

        if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2, 100, 30), "Test生成"))
        {
            if (!_IsLanModeActive) return;

            NetworkObjectSpawner.Instance.RequestSpawnObjectRandomInRange2D(
                "Daifuku", new Vector3(715f, 8f, 12f), 5f, 8f, Quaternion.identity, NetworkObjectSpawner.OwnerMode.Host);
        }
    }

    void LateUpdate()
    {
        // Pad開始やUI切替は LocalPadSession 側でやる
    }

    private void PlayerTeleportAndConnect(Vector3 pos, ulong id)
    {
        List<GameObject> players = new List<GameObject>();

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            if (playerRef.TryGet(out var playerObj))
            {
                if (playerObj.gameObject.CompareTag("Player") && playerObj.OwnerClientId == id)
                {
                    players.Add(playerObj.gameObject);
                }
            }
        }

        if (players.Count <= 0) return;

        for (int i = 0; i < players.Count; i++)
        {
            Vector3 newPos = pos;
            newPos.z = newPos.z + (i * -1f);
            var netTrans = players[i].GetComponent<NetworkTransform>();
            netTrans.Teleport(newPos, netTrans.gameObject.transform.rotation, netTrans.gameObject.transform.localScale);
        }

        if (players.Count <= 1) return;

        foreach (var ropeRef in GameManager.Instance._networkObjectList)
        {
            if (ropeRef.TryGet(out var ropeObj))
            {
                if (ropeObj.gameObject.CompareTag("Rope") && ropeObj.OwnerClientId == id)
                {
                    StartCoroutine(ropeObj.gameObject.GetComponent<PlayerJoint>().DelayConnect());
                }
            }
        }
    }
}
