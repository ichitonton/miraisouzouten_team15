using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Runtime.InteropServices;



public class PlayerNetworkConnect : NetworkBehaviour
{

    [TextArea(2, 5)]
    public string memo;


    [Header("NetworkObject付きの生成するPlayer1")]
    [SerializeField] private GameObject _playerObject1; // Hostが生成する用
    [Header("NetworkObject付きの生成するPlayer2")]
    [SerializeField] private GameObject _playerObject2;

    [Header("接続してからプレイヤーを生成するときの遅延")]
    public float _delayTime = 0.1f;

    private int playerCount = 0;

    private bool _didReplace = false;

    private void Start()
    {
        var nm = NetworkManager.Singleton;

        

        nm.OnServerStarted += OnHostStarted;
        nm.OnClientConnectedCallback += OnClientConnected;

        if (nm.IsServer && !GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[Host] PlayerNetworkConnect Spawned on Network");
        }
    }

    private void  OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnHostStarted;
            //Hostは新しいClientが自分のところに接続したときに呼ばれ、ClientはHostに接続できたときに呼ばれる
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    /// <summary>
    /// Hostとして接続したときに呼ばれる
    /// /// </summary>
    private void OnHostStarted()
    {
        // ローカルプレイヤーを削除し、同じ位置にNetworkプレイヤーを再生成して登録する
        ReplaceLocalPlayersWithNetworkPlayers();
    }

    /// <summary>
    /// Client接続時に呼ばれる（Hostで実行）Clientのプレイヤーを生成
    /// /// </summary>
    private void OnClientConnected(ulong clientId)
    {

        var nm = NetworkManager.Singleton;

        // Host側：Clientが接続してきたときに実行される
        if (nm.IsServer)
        {
            // Host自身のClientIdも通るが、Hostが自分で自分を処理する必要はない
            if (clientId == nm.LocalClientId)
            {
                Debug.Log("[Host] 自分（Host）が接続したのでスキップ");
                return;
            }

            Debug.Log($"[Host] Client {clientId} が接続しました（Host側）");
            
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                StartCoroutine(DelayedPlayerReplace());
            }
        }

    }

    private IEnumerator DelayedPlayerReplace()
    {
        if (_didReplace) yield break;   // ← 二重実行を防止！！

        _didReplace = true;

        yield return new WaitForSeconds(_delayTime); // ← 接続安定化のため少し待つ
        ReplaceLocalPlayersWithNetworkPlayers();
    }

    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]//このRpcを使えばClientから要求ができるからあとからClient側から追加することも可能
    private void RequestPlayerSpawnServerRpc(ulong clientId, Vector3 pos, Quaternion rot,int count)
    {

        Debug.Log($"[RPC Called] IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}, Mode={NetworkManager.Singleton.IsListening}");

        //Hostでしか実行されないようにする
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] Clientで誤実行されたためスキップ");
            return;
        }
        
        Debug.Log($"[Host] Client {clientId} からPlayer生成リクエストを受信");

        GameObject newPlayer = default;

        pos.y += 1f;

        //カウントの数字を見て生成するプレイヤーを分ける
        if (count == 1)
            newPlayer = Instantiate(_playerObject1, pos, rot);
        else if (count == 2)
            newPlayer = Instantiate(_playerObject2, pos, rot);

        Debug.Log("プレイヤー" + count + "を生成");

        GameManager.Instance._objectList.Add(newPlayer);
        
        var netObj = newPlayer.GetComponent<NetworkObject>();
        //Network上に登録するときにそのプレイヤーの所有権を決めれる(ClientID)
        //Netcode では 「所有権（ownership）」＝ そのオブジェクトの処理をどこで実行するかの主導権を決める仕組み
        //「このオブジェクトの動作や入力をどのクライアントが担当するか」が明確に決まります。
        //またDefaltPlayerを上書きするからプレイヤーのオブジェクトにのみ有効
        netObj.SpawnAsPlayerObject(clientId);

        //ネットワークへの登録が終わったらネットワーク上で共有されるNetworkListに登録
        //これでどのClientから見ても同じものを参照できるよ
        GameManager.Instance._networkObjectList.Add(new NetworkObjectReference(netObj));
        
        
    }


    /// <summary>
    /// ローカルプレイヤーを削除し、同じ位置にNetworkプレイヤーを再生成して登録する
    /// </summary>
    private void ReplaceLocalPlayersWithNetworkPlayers()
    {
        var localPlayers = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log("ローカルのプレイヤーの数" + localPlayers.Length);
        int count = 0;

        foreach (var lp in localPlayers)
        {

            // NetworkObjectがすでにあるならスキップ
            if (lp.TryGetComponent<NetworkObject>(out var netObj))
            {
                Debug.Log($"[Network] 既にNetwork化されている: {lp.name}");
                continue;
            }


            //プレイヤーの数を加算
            count++;

            // Transform情報を保持
            Vector3 pos = lp.transform.position;
            Quaternion rot = lp.transform.rotation;

            Debug.Log($"[Network] Local PlayerをNetwork上に再生成: {lp.name} at {pos}");
            //Hostに自分の生成を依頼（ServerRpc）

            RequestPlayerSpawnServerRpc(NetworkManager.Singleton.LocalClientId, pos, rot, count);

            // 元のローカルオブジェクトを削除// NetworkObjectを壊してよいのはホストだけ
            if (IsServer)
            {
                Destroy(lp);
            }
            else
            {
                // クライアントはローカル用の Player なら DestroyOK
                if (!lp.TryGetComponent<NetworkObject>(out _))
                {
                    Destroy(lp); // NetworkObjectなしならOK
                }
            }

            //リストからも消す
            GameManager.Instance._objectList.Remove(lp);
        }

    }

}
