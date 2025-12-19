using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;
using UnityEditor;


public class NetworkDebug : MonoBehaviour
{

    private UnityTransport _transport;
    private string _localIP;
    private string _status = "Idle";


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _transport = GetComponent<UnityTransport>();
        _localIP = GetLocalIPAddress();

        // èÛë‘ïœçXÉCÉxÉìÉgìoò^
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisConnected;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;

        

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnGUI()
    {

        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(400, 10, 300, 200), GUI.skin.box);

        GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsServer ? "Host/Server" : NetworkManager.Singleton.IsClient ? "Client" : "None")}");
        GUILayout.Label($"Local IP: {_localIP}");
        GUILayout.Label($"Port: {_transport.ConnectionData.Port}");
        GUILayout.Label($"Address: {_transport.ConnectionData.Address}");
        GUILayout.Label($"Clients Connected: {NetworkManager.Singleton.ConnectedClients.Count}");
        GUILayout.Label($"Status: {_status}");

        var scene = SceneManager.GetActiveScene();

        GUILayout.Label($"ScenaName: {scene.name}");

        GUILayout.EndArea();
    }


    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisConnected;
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;

    }

    private void OnClientConnected(ulong clientID)
    {
        _status = $"Client Connected (ID: {clientID})";
    }

    private void OnClientDisConnected(ulong clientID)
    {
        _status = $"Client Disconnected (ID: {clientID})";
    }

    private void OnServerStarted()
    {
        _status = "Server Start";
    }


    private string GetLocalIPAddress()
    {
        try
        {
            string hostname = Dns.GetHostName();
            var address = Dns.GetHostAddresses(hostname);
            foreach(var ip in address)
            {
                if(ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
            return "Unknown";
        }
        return "Unknown;";
    }


}
