using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;


public class GameManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private GameObject _ropeObject;
    private GameObject _rope = null;
    private GameObject[] _players;

    private void OnEnable()
    {


        SceneManager.sceneLoaded += RegisterNetworkConnectEvent;
        
        
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
