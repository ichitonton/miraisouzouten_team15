using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{
    public static TeamManager Instance { get; private set; }

    // ClientId → TeamId
    private Dictionary<ulong, int> clientToTeam = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnClientConnected(ulong clientId)
    {
        // 仮：接続順でチーム割り当て（あとで自由に変えられる）
        int teamId = clientToTeam.Count % 3; // 3チーム想定
        clientToTeam[clientId] = teamId;

        Debug.Log($"[TeamManager] ClientId={clientId} → TeamId={teamId}");
    }

    void OnClientDisconnected(ulong clientId)
    {
        clientToTeam.Remove(clientId);
    }

    // ★ ここが一番大事
    public int GetTeamIdByClientId(ulong clientId)
    {
        if (!clientToTeam.TryGetValue(clientId, out int teamId))
        {
            Debug.LogError($"TeamId not found for ClientId={clientId}");
            return -1;
        }
        return teamId;
    }
}
