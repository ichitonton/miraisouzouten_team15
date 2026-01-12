using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{
    public static TeamManager Instance { get; private set; }
    [SerializeField] YajirushiToGoal _yajirushi;

    // ClientId Å® TeamId
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

        _yajirushi.SetTargetGoal(OwnerClientId);
    }
    void OnClientConnected(ulong clientId)
    {
        int teamId = clientToTeam.Count % 3;
        clientToTeam[clientId] = teamId;

        SetGoalClientRpc(clientId, teamId);
    }

    [ClientRpc]
    void SetGoalClientRpc(ulong clientId, int teamId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId)
            return;

        var yajirushi = FindFirstObjectByType<YajirushiToGoal>();
        yajirushi.SetTargetGoal((ulong)teamId);
    }


    void OnClientDisconnected(ulong clientId)
    {
        clientToTeam.Remove(clientId);
    }

    // Åö Ç±Ç±Ç™àÍî‘ëÂéñ
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
