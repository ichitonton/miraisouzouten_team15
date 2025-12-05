using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;


public class GameManager : NetworkBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private GameObject _ropeObject;
    [SerializeField] private GameObject _ui;
    [Header("ローカル内で動くやつだからNetworkObjectついてないプレイヤー入れてね")]
    [SerializeField] private GameObject _player1;
    [SerializeField] private GameObject _player2;
    [SerializeField] private Transform _pivot;
       
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
    

    private void OnGUI()
    {


        

        /*if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 100, 120, 30), "プレイヤー1生成"))
        {
            List<GameObject> players = _objectList.FindAll(obj => obj.CompareTag("Player"));

            if (players.Count >= 2)
            {
                Debug.Log("プレイヤー二人もういますけど");
                return;
            }

            GameObject player = Instantiate(_player1, _pivot.position, Quaternion.identity);
            _objectList.Add(player);
        }*/

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
        /*if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2, 100, 30), "Test生成"))
        {
            if (!_IsLanModeActive) return;

            //NetworkObjectSpawner.Instance.RequestSpawnObject("KintaroAme", new Vector3(0f,10f,0f),Quaternion.identity, NetworkObjectSpawner.OwnerMode.Host);
            NetworkEffectSpawner.Instance.PlayEffect(0, new Vector3(0f, 5f, 0f), Quaternion.identity);

        }*/

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

            GameObject player = Instantiate(_player1, _pivot.position, Quaternion.identity);
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

            GameObject player = Instantiate(_player2, _pivot.position, Quaternion.identity);
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

}
