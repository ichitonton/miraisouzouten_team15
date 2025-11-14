using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RopeSpawner : NetworkBehaviour
{

    [SerializeField] private GameObject _ropeObject = default;
    private PlayerNetworkConnect _playerNetworkConnect = null;
    [SerializeField] private float _delayTime = 0.1f;
    [SerializeField] private Transform _pivot;
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
        Debug.Log("Host : Ropeを作る");
        //ホストは自分自身にリクエストを送る
        StartCoroutine(DelayRequestRope(NetworkManager.Singleton.LocalClientId));
    }

    private void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;

        // Host側：Clientが接続してきたときに実行される
        if (nm.IsServer)
        {
            // Host自身のClientIdも通るが、Hostが自分で自分を処理する必要はない
            if (clientId == nm.LocalClientId)
            {
                Debug.Log("[Host] Rope自分（Host）が接続したのでスキップ");
                return;
            }

            Debug.Log($"[Host] Rope Client {clientId} が接続しました（Host側）");

            return;
        }

        // Client側：Hostへの接続完了
        if (nm.IsClient && !nm.IsServer)
        {
            Debug.Log($"[Client] Hostに接続完了: {clientId}");
            //このclientIdからリクエストを送ったよ
            StartCoroutine(DelayRequestRope(clientId));
        }


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
        GameObject rope = Instantiate(_ropeObject, _pivot.position, Quaternion.identity);
        var netObj = rope.GetComponent<NetworkObject>();
        //オブジェクトのオーナーを決める
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
            //ropeObj.gameObject.GetComponent<ConnectPlayers>().Connect();
            
        }
    }




}
