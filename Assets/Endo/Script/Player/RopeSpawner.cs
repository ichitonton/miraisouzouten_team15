using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class RopeSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject _ropeObject = default;

    private PlayerNetworkConnect _playerNetworkConnect = null;

    [SerializeField] private float _delayTime = 0.2f;

    [SerializeField] private Transform _pivotId0;
    [SerializeField] private Transform _pivotId1;
    [SerializeField] private Transform _pivotId2;

    private bool _subscribed = false;

    // NetworkBehaviourは Start より OnNetworkSpawn が安全
    public override void OnNetworkSpawn()
    {
        _playerNetworkConnect = GetComponent<PlayerNetworkConnect>();

        SubscribeEvents();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeEvents();
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        // 念のため（シーン破棄などで Despawn を通らない時もある）
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_subscribed) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnServerStarted += OnHostStarted;
        nm.OnClientConnectedCallback += OnClientConnected;

        _subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_subscribed) return;

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted -= OnHostStarted;
            nm.OnClientConnectedCallback -= OnClientConnected;  //  ここが一番大事（修正点）
        }

        _subscribed = false;
    }

    private void OnHostStarted()
    {
        if (!IsSpawned) return;

        Debug.Log("Host : Ropeを作る");

        // Hostは自分自身にリクエスト
        StartCoroutine(DelayRequestRope(NetworkManager.Singleton.LocalClientId));
    }

    private void OnClientConnected(ulong clientId)
    {
        // 破棄済みなら即return（念のため）
        if (!this || !isActiveAndEnabled) return;
        if (!IsSpawned) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Host側：Clientが接続してきた
        if (nm.IsServer)
        {
            // Host自身の接続通知はスキップ
            if (clientId == nm.LocalClientId)
            {
                Debug.Log("[Host] Rope 自分（Host）の接続通知なのでスキップ");
                return;
            }

            Debug.Log($"[Host] Rope Client {clientId} が接続しました");
            // ※ここで何かするなら Host側で処理
            return;
        }

        // Client側：Hostに接続完了
        if (nm.IsClient && !nm.IsServer)
        {
            Debug.Log($"[Client] Hostに接続完了: {clientId}");
            StartCoroutine(DelayRequestRope(clientId));
        }
    }

    private IEnumerator DelayRequestRope(ulong clientId)
    {
        // 接続途中に破棄される可能性があるのでガード
        if (_playerNetworkConnect == null)
            yield break;

        yield return new WaitForSeconds(_playerNetworkConnect._delayTime + _delayTime);

        if (!IsSpawned) yield break;
        if (NetworkManager.Singleton == null) yield break;
        if (!NetworkManager.Singleton.IsListening) yield break;

        RequestRopeSpawnServerRpc(clientId);
    }

    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestRopeSpawnServerRpc(ulong requestClientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        int playerCount = 0;

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            if (playerRef.TryGet(out var playerObj))
            {
                if (playerObj != null &&
                    playerObj.gameObject.CompareTag("Player") &&
                    playerObj.OwnerClientId == requestClientId)
                {
                    playerCount++;
                    playerObj.GetComponent<MovePlayerKey>().GameStartUseItem();
                }
            }
        }

        if (playerCount < 2)
        {
            Debug.Log("ロープでつなごうとしたけど、プレイヤー二人いなかった");
            return;
        }

        Debug.Log($"[Host] Client {requestClientId} からRope生成リクエストを受信");

        // pivot選択だけ %3（ownerは絶対にいじらない）
        Transform pivot = GetPivotByClientId(requestClientId);

        GameObject rope = Instantiate(_ropeObject, pivot.position, Quaternion.identity);
        var netObj = rope.GetComponent<NetworkObject>();

        // 所有権は requestClientId のまま
        netObj.SpawnWithOwnership(requestClientId);

        GameManager.Instance._networkObjectList.Add(new NetworkObjectReference(netObj));

        //送信先も requestClientId のまま
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { requestClientId }
            }
        };

        OnConnectClientRpc(netObj.NetworkObjectId, rpcParams);
    }

    private Transform GetPivotByClientId(ulong clientId)
    {
        ulong mod = clientId % 3;

        if (mod == 0) return _pivotId0;
        if (mod == 1) return _pivotId1;
        return _pivotId2;
    }

    [ClientRpc]
    private void OnConnectClientRpc(ulong ropeNetworkId, ClientRpcParams clientRpcParams = default)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.SpawnManager.SpawnedObjects.TryGetValue(ropeNetworkId, out var ropeObj))
        {
            Debug.Log($"[ClientRpc] Rope({ropeNetworkId}) の Connect を実行");
            // ropeObj.GetComponent<ConnectPlayers>()?.Connect();
        }
    }
}
