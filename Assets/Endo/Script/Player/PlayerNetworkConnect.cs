using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using UnityEngine.SceneManagement;



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

    [Tooltip("このシーン名になったときに InGame を監視する")]
    [SerializeField] private string gameSceneName = "GameScene";

    private bool _hooked = false;

    //private void Start()
    //{

    //    var active = SceneManager.GetActiveScene();
    //    if (!active.IsValid()) return;

    //    // Scene名が一致しているか？
    //    if (!string.Equals(active.name, gameSceneName)) return;

    //    var nm = NetworkManager.Singleton;



    //    nm.OnServerStarted += OnHostStarted;
    //    nm.OnClientConnectedCallback += OnClientConnected;

    //    if (nm.IsServer && !GetComponent<NetworkObject>().IsSpawned)
    //    {
    //        GetComponent<NetworkObject>().Spawn(true);
    //        Debug.Log("[Host] PlayerNetworkConnect Spawned on Network");
    //    }
    //}



    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        EvaluateAndHook(SceneManager.GetActiveScene());
        //DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Unhook();
        Debug.Log("コネクト、死んだん？");
        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            List<GameObject> players = new List<GameObject>();

            foreach (var playerRef in GameManager.Instance._networkObjectList)
            {
                if (playerRef.TryGet(out var playerObj))
                {
                    if (playerObj.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                    {
                        players.Add(playerObj.gameObject);
                    }
                }
            }

            foreach (var player in players)
            {

                Debug.Log(player.name);

                player.gameObject.GetComponent<NetworkObject>().Despawn(true);
                Destroy(player.gameObject);
            }

        }
    }

    private void OnDestroy()
    {
        // 念のため保険（OnDisableが呼ばれない状況もある）
        Unhook();
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        EvaluateAndHook(next);

        Debug.Log($"Scene Changed: {prev.name} -> {next.name}");

    }

    private void EvaluateAndHook(Scene active)
    {
        if (!active.IsValid()) return;

        //  対象シーンじゃないなら解除
        if (!string.Equals(active.name, gameSceneName))
        {
            Unhook();
            return;
        }

        //  対象シーンなら購読
        HookOnce();

        //  HostならここでSpawn（必要なら）
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && !GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Spawn(true);
            Debug.Log("[Host] PlayerNetworkConnect Spawned on Network");
        }
    }

    private void HookOnce()
    {
        if (_hooked) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnServerStarted += OnHostStarted;
        nm.OnClientConnectedCallback += OnClientConnected;
        _hooked = true;

        Debug.Log("[PlayerNetworkConnect] Hooked callbacks");
    }

    private void Unhook()
    {
        if (!_hooked) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) { _hooked = false; return; }

        nm.OnServerStarted -= OnHostStarted;
        nm.OnClientConnectedCallback -= OnClientConnected;
        _hooked = false;

        Debug.Log("[PlayerNetworkConnect] Unhooked callbacks");
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
