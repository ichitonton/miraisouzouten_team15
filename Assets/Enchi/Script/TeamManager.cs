using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{
    public static TeamManager Instance { get; private set; }

    // ClientId → TeamId
    private readonly Dictionary<ulong, int> clientToTeam = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        // リスタート時にInstance残り事故を防ぐ
        if (Instance == this) Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;

        // すでに接続してるクライアント（Host自身含む）も処理したいなら
        foreach (var c in nm.ConnectedClientsIds)
        {
            OnClientConnected(c);
        }
    }

    public override void OnNetworkDespawn()
    {
        //  絶対解除（イベント残り事故防止）
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        clientToTeam.Clear();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        //  チーム割り当て（すでにあるなら再送だけ）
        if (!clientToTeam.TryGetValue(clientId, out int teamId))
        {
            teamId = clientToTeam.Count % 3;
            clientToTeam[clientId] = teamId;
        }

        //  ここが重要：即RPCせず “1フレーム遅延”
        StartCoroutine(SendGoalNextFrame(clientId, teamId));
    }

    private IEnumerator SendGoalNextFrame(ulong clientId, int teamId)
    {
        yield return null; // 1フレ待つ（これで __endSendClientRpc の事故が激減する）

        var nm = NetworkManager.Singleton;
        if (nm == null) yield break;
        if (!nm.IsListening) yield break;
        if (!IsSpawned) yield break;

        //  接続中のクライアントだけ
        if (!nm.ConnectedClientsIds.Contains(clientId)) yield break;

        //  そのクライアントだけに送る（ムダ撃ち防止 & 安全）
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        SetGoalClientRpc(teamId, rpcParams);
    }

    [ClientRpc]
    private void SetGoalClientRpc(int teamId, ClientRpcParams clientRpcParams = default)
    {
        // ここは「受け取った本人だけ」に届くので LocalClientId 判定は不要

        // シーン読み込み直後だとまだ見つからない事があるので安全に探す
        var yajirushi = FindFirstObjectByType<YajirushiToGoal>();
        if (yajirushi == null)
        {
            Debug.LogWarning("[TeamManager] YajirushiToGoal not found yet.");
            return;
        }

        yajirushi.SetTargetGoal((ulong)teamId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        clientToTeam.Remove(clientId);
    }

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
