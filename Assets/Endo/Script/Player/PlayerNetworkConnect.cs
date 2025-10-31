using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class PlayerNetworkConnect : MonoBehaviour
{

    private bool _isBound = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void Awake()
    {
        
    }

    void Start()
    {
        
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= TryBindLocalPlayer;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    public void InitPlayerNetwork()
    {

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("NetworkManagerが見つかりません");
            return;
        }

        nm.OnServerStarted += TryBindLocalPlayer;
        nm.OnClientConnectedCallback += OnClientConnected;

        Debug.Log($"[Binder Init] IsHost={NetworkManager.Singleton.IsHost}, IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}");
    }
    /// <summary>
    /// Hostが起動したとき、自分のシーン内にあるすべてのPlayerをSpawn
    /// </summary>
    private void TryBindLocalPlayer()
    {
        if (_isBound) return;

        // シーン上の全Playerを取得（タグ or コンポーネントベースで）
        var allPlayers = GameObject.FindGameObjectsWithTag("Player");

        if (allPlayers.Length == 0)
        {
            Debug.LogWarning("シーン内にPlayerが見つかりません。");
            return;
        }

        foreach (var player in allPlayers)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            if (netObj.IsSpawned) continue;

            // HostのPlayerを登録
            netObj.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
            Debug.Log($"[Host] 自分のPlayerをSpawn: {player.name}");
        }

        _isBound = true;

    }

    /// <summary>
    /// Client接続時に呼ばれる（Hostで実行）
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        // Host側でClientのPlayerを探して紐づける処理
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("クライアント接続");

            // シーン内のPlayerから、まだSpawnされていないものを探す
            var allPlayers = GameObject.FindGameObjectsWithTag("Player");

            foreach (var player in allPlayers)
            {
                var netObj = player.GetComponent<NetworkObject>();
                if (netObj == null || netObj.IsSpawned) continue;

                // ClientのPlayerを登録
                netObj.SpawnAsPlayerObject(clientId);
                Debug.Log($"[Host] Client {clientId} のPlayerをSpawn: {player.name}");
                return; // 一人分でOK
            }

            Debug.LogWarning($"[Host] Client {clientId} に対応するPlayerが見つかりません。");
        }
        // Client側のとき
        else if (NetworkManager.Singleton.IsClient)
        {
            Debug.Log($"[Client] Hostへの接続が完了しました。ClientId: {clientId}");

        }

        
    }
}
