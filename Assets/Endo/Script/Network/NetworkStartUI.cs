using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;

public class NetworkStartUI : MonoBehaviour
{
    [SerializeField] private GameObject _net;
    private UnityTransport _transport;
    private string _ipAddress;

    private void Awake()
    {
        _transport = _net.GetComponent<UnityTransport>();
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
            ////ホストとして入る
            //if (GUILayout.Button("ホストとして接続"))
            //{
            //    _net.GetComponent<LanHost>().StartHostConnect();
            //    //_net.GetComponent<PlayerNetworkConnect>().InitPlayerNetwork();
                
            //    //_net.GetComponent<LanHostDiscovery>().StartHostConnect();

            //}
            ////クライアントとして入る
            //if (GUILayout.Button("ローカルLAN内のIPを自動取得してClientとして接続"))
            //{
            //    _net.GetComponent<LanClient>().StartClientConnect();
            //    //_net.GetComponent<PlayerNetworkConnect>().InitPlayerNetwork();

            //    //_net.GetComponent<LanClientDiscovery>().StartClientConnect();

            //}
        }

    }

    private void LateUpdate()
    {

        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                _net.GetComponent<LanHost>().StartHostConnect();
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                _net.GetComponent<LanClient>().StartClientConnect();
            }
        }

        
    }

}
