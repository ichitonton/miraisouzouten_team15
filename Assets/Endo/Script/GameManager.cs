using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


public class GameManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private GameObject _ropeObject;
    [SerializeField] private GameObject _ui;
    private GameObject _rope = null;
    [SerializeField] private GameObject _player;
    private GameObject[] _players;
    private GameObject _networkUi;

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
        }
        Application.targetFrameRate = 60;
    }
    private void Start()
    {
        //if (_ui != null)
        //{
        //    //UIを出す
        //    if (_ui.activeSelf == false)
        //    {
        //        _ui.SetActive(true);
        //    }
        //    Debug.Log("ボタンを押してはよ入れや");
        //    _ui.GetComponentInChildren<TMP_Text>().text = "2 Player Not Join";
        //}
    }
    private void OnEnable()
    {


        //SceneManager.sceneLoaded += RegisterNetworkConnectEvent;
        
        
    }

    private void OnGUI()
    {
        //ホストとして入る
        if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2, 100, 30), "ボタン"))
        {
            Instantiate(_player, new Vector3(0f, 5.0f, 0f), Quaternion.identity);
        }
    }
    // Update is called once per frame
    void LateUpdate()
    {

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
                

            Connect();
        }
    }

    private void Connect()
    {
        _players = GameObject.FindGameObjectsWithTag("Player");

        if (_players.Length >= 2 && _rope == null)
        {
            //プレイヤーをつなげる
            _rope = Instantiate(_ropeObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
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
