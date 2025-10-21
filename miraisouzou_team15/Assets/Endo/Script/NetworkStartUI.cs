using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkStartUI : MonoBehaviour
{

    private UnityTransport _transport;

    private void Awake()
    {
        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnGUI()
    {

        if (NetworkManager.Singleton == null)
        {
            GUILayout.Label("NetworkManager not found in scene!");
            return;
        }

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            //ホストとして入る
            if (GUILayout.Button("Host"))
            {
                _transport.SetConnectionData("0.0.0.0", 7777); // どのIPからの接続も受け入れる
                NetworkManager.Singleton.StartHost();
            }
            //クライアントとして入る
            if (GUILayout.Button("Client"))
            {
                _transport.SetConnectionData("192.168.0.1", 7777);
                NetworkManager.Singleton.StartClient();
            }
        }

    }
}
