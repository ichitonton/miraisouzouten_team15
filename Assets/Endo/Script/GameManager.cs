using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;
using Unity.Netcode.Components;


public class GameManager : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private GameObject _ropeObject;
    [SerializeField] private GameObject _ui;
    [Header("ローカル内で動くやつだからNetworkObjectついてないプレイヤー入れてね")]
    [SerializeField] private GameObject _player1;
    [SerializeField] private GameObject _player2;

    [Header("SpawnPoint")]
    [SerializeField] private Transform _spawnPos;
    [Header("StartPosition")]
    [SerializeField] private Transform[] _pivot;
       
    private GameObject[] _players;
    private GameObject _networkUi;

    public bool _IsLanModeActive => NetworkManager.Singleton.IsListening;

    // シングルトンのグローバルなアクセスポイント (public static)
    public static GameManager Instance { get; private set; }

    public enum Mode
    {
        Keyboard,
        Gamepad
    }

    public enum OnlineMode
    {
        OnePC,
        MoreTowPC
    }

    [Header("操作モード設定")]
    public Mode _controlMode = Mode.Keyboard;

    [Header("通信モード")]
    public OnlineMode _onlineMode = OnlineMode.OnePC;

    public List<GameObject> _objectList = new List<GameObject>();


    //ネットワークオブジェクトのリスト
    public NetworkList<NetworkObjectReference> _networkObjectList = new NetworkList<NetworkObjectReference>();

    //ゲームスタートしたかどうか
    private bool _isStart = false;
    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        Application.targetFrameRate = 60;
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
            if (_pivot[i] == null) continue;
            PlayerTeleportAndConnect(_pivot[i].position, (ulong)i);
            //ふわふわBGMを全Clientで流す&ループあり
            NetworkSoundManager.Instance.PlayBgm("FuwaFuwa", NetworkSoundManager.SoundScope.AllClients, true);
            _isStart = true;
        }
    }

    private void OnGUI()
    {
        if (_isStart) return;
        if (NetworkManager.Singleton == null) return; 
        if (!NetworkManager.Singleton.IsServer) return;

        if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 100, 120, 30), "ゲームスタート"))
        {
            PlayMovieClientRpc();
            StartCoroutine("TeleportPlayer", 0.3f);
            //TeleportPlayer();

        }

        //ホストとして入る
        /*if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 50, 120, 30), "プレイヤー2生成"))
        {
            List<GameObject> players = _objectList.FindAll(obj => obj.CompareTag("Player"));

            if (players.Count >= 2)
            {
                Debug.Log("プレイヤー二人もういますけど");
                return;
            }

            GameObject player = Instantiate(_player2, _pivot.position, Quaternion.identity);
            _objectList.Add(player);
        }*/

        //生成
        if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2, 100, 30), "Test生成"))
        {
            if (!_IsLanModeActive) return;

            NetworkObjectSpawner.Instance.RequestSpawnObjectRandomInRange2D("Daifuku",new Vector3(715f,8f,12f),5f,8f,Quaternion.identity,NetworkObjectSpawner.OwnerMode.Host);
            //NetworkEffectSpawner.Instance.PlayEffect(0, new Vector3(0f, 5f, 0f), Quaternion.identity);

        }

        //生成
        /*if (GUI.Button(new Rect(1000f, 100f, 100, 30), "マップ表示"))
        {

            bool active = !_ui.gameObject.activeSelf;
           _ui.gameObject.SetActive(active);

        }*/

    }
    // Update is called once per frame
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            List<GameObject> players = _objectList.FindAll(obj => obj.CompareTag("Player"));

            if (players.Count >= 2)
            {
                Debug.Log("プレイヤー二人もういますけど");
                return;
            }

            GameObject player = Instantiate(_player1, _spawnPos.position, Quaternion.identity);
            Debug.Log("わいた");
            _objectList.Add(player);

        }

        else if(Input.GetKeyDown(KeyCode.RightShift))
        {
            List<GameObject> players = _objectList.FindAll(obj => obj.CompareTag("Player"));

            if (players.Count >= 2)
            {
                Debug.Log("プレイヤー二人もういますけど");
                return;
            }

            GameObject player = Instantiate(_player2, _spawnPos.position, Quaternion.identity);
            _objectList.Add(player);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client Connected: {clientId}");
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client Connected: {clientId}");
        
        // ここで「プレイヤーが二人になった瞬間」に処理を入れられる
        if (NetworkManager.Singleton.ConnectedClients.Count == 2)
        {
            Debug.Log("2人そろった！");

            if (_ui != null)
            {
                //UIを出す
                if (_ui.activeSelf == true)
                {
                    _ui.SetActive(false);
                }
            }
            if (_ui != null)
            {
                if(_networkUi.activeSelf == false)
                {
                    _networkUi.SetActive(true);
                }
            }
               
        }
    }

    private void RegisterNetworkConnectEvent(Scene scene, LoadSceneMode mode)
    {

        if(NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientDisconnected;
        }
        
    }

    private void PlayerTeleportAndConnect(Vector3 pos,ulong id)
    {

        List<GameObject> players = new List<GameObject>();

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if (playerObj.gameObject.CompareTag("Player") && playerObj.OwnerClientId == id)
                {
                    Debug.Log("所有権を持ったプレイヤーです");
                    players.Add(playerObj.gameObject);
                }
            }


        }

        Debug.Log("テレポートするプレイヤーの数" + players.Count);

        if (players.Count <= 0)
        {
            Debug.Log("テレポートさせるプレイヤーがいませんでした");
            return;
        }

        for(int i = 0; i < players.Count; i++)
        {
            Vector3 newPos = pos;
            newPos.z = newPos.z + (i * -1f);
            var netTrans = players[i].GetComponent<NetworkTransform>();
            netTrans.Teleport(newPos, netTrans.gameObject.transform.rotation, netTrans.gameObject.transform.localScale);
        }


        //プレイヤーが二人いなかったらロープをつながない
        if (players.Count <= 1)
        {
            Debug.Log("ボッチやぞ");
            return;
        }

        foreach (var ropeRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (ropeRef.TryGet(out var ropeObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if (ropeObj.gameObject.CompareTag("Rope") && ropeObj.OwnerClientId == id)
                {
                    Debug.Log("所有権のあるあみです");
                    //少し待ってからロープでつなぐ
                    StartCoroutine(ropeObj.gameObject.GetComponent<PlayerJoint>().DelayConnect());
                }
            }
        }

    }

}
