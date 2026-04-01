using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class Sensour : NetworkBehaviour
{
    [Header("Detect Settings")]
    [SerializeField] float detectRadius = 5f;
    [SerializeField] float detectInterval = 0.2f;

    NavMeshAgent _agent;
    float _timer;

    readonly List<Transform> _players = new();

    void Awake()
    {
        _agent = GetComponentInParent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {
        // ★ Serverのみで動かす
        if (!IsServer)
            enabled = false;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < detectInterval) return;
        _timer = 0f;

        UpdatePlayers();
    }

    void UpdatePlayers()
    {
        _players.Clear();

        // プレイヤー全取得（キャッシュしてもOK）
        var players = FindObjectsByType<MovePlayerKey>(
            FindObjectsSortMode.None);

        foreach (var player in players)
        {
            float dist = Vector3.Distance(
                _agent.transform.position,
                player.transform.position
            );

            if (dist <= detectRadius)
            {
                _players.Add(player.transform);
            }
        }
    }

    // ★ 外部からは ReadOnly で取得
    public IReadOnlyList<Transform> GetPlayers()
    {
        return _players;
    }
}
