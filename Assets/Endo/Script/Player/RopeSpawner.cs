using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RopeSpawner : NetworkBehaviour
{

    [SerializeField] private GameObject _ropeObject = default;
    private PlayerNetworkConnect _playerNetworkConnect = null;
    [SerializeField] private float _delayTime = 0.1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _playerNetworkConnect = GetComponent<PlayerNetworkConnect>();
        NetworkManager.Singleton.OnServerStarted += OnHostStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnHostStarted()
    {
        
    }

    private void OnClientConnected(ulong clientId)
    {

       

    }

    private IEnumerator DelayRequestRope(ulong clientId)
    {
        yield return new WaitForSeconds(_playerNetworkConnect._delayTime + _delayTime);
        RequestRopeSpawnServerRpc(clientId);
    }


    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestRopeSpawnServerRpc(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] ClientでRope誤実行されたためスキップ");
            return;
        }

        int playerCount = 0;

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーかつそのクライアントのモノかどうか
                if (playerObj.gameObject.CompareTag("Player")&& playerObj.OwnerClientId == clientId)
                {
                    playerCount++;
                }
            }
        }

        if (playerCount < 2)
        {
            Debug.Log("ロープでつなごうとしたけど、プレイヤー二人いなかった");
            return;
        }


        Debug.Log($"[Host] Client {clientId} からRope生成リクエストを受信");

        // Ropeを生成
        GameObject rope = Instantiate(_ropeObject, Vector3.zero, Quaternion.identity);
        var netObj = rope.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(clientId);

        // ClientRpcの送信先を1クライアントに限定
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId } // ← ここで送信先を指定！
            }
        };

        // そのクライアントにだけ OnConnect を通知
        OnConnectClientRpc(netObj.NetworkObjectId, rpcParams);
    }



    private IEnumerator DelayRequestConnect(ulong clientId)
    {
        yield return new WaitForSeconds(_playerNetworkConnect._delayTime + _delayTime);

    }


    //クライアント側で実行されるHost側からきたリクエスト
    [ClientRpc]
    private void OnConnectClientRpc(ulong ropeNetworkId, ClientRpcParams clientRpcParams = default)
    {
        //これでNetwrokObjectの固有id検索によりRopeが識別できる。
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ropeNetworkId, out var ropeObj))
        {
            Debug.Log($"[ClientRpc] Rope({ropeNetworkId}) の Connect.OnConnect() を実行");
            //プレイヤーをつなぐ処理
            ropeObj.gameObject.GetComponent<ConnectPlayers>().Connect();
            
        }
    }




}
