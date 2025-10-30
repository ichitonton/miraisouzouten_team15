using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkSpawner : MonoBehaviour
{

    [SerializeField] private GameObject _playerPrefab;
    [Header("プレイヤーをスポーンさせる位置")]
    [SerializeField] private Vector3[] _spawnPoints;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
    }

    private void OnServerStarted()
    {
        // 自動生成OFFにしている場合のみ必要
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // プレイヤーを生成

        Vector3 spawnPos = _spawnPoints[(int)clientId];

        var player = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
