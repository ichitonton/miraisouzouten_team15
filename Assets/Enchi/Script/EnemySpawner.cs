using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] GameObject _enemy;
    GameObject _enemySave;
    [SerializeField] float _respawnDelay = 5.0f;
    List<Transform> _players = new List<Transform>();

    Transform _kari;
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        GenerateEnemyServerRpc();
    }

    public void EnemyIsDead()
    {
        Invoke("GenerateEnemyServerRpc", _respawnDelay);
    }



    [ServerRpc(RequireOwnership = false)]
    void GenerateEnemyServerRpc()
    {
        if (!IsServer) return; // ← これが必須
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        NetworkObject obj = _ObjectPool.Get(_enemy.GetComponent<NetworkObject>(), transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_enemy.GetComponent<NetworkObject>());
        obj.gameObject.GetComponent<Jibaku>().SetSpawner(GetComponent<NetworkObject>());
        Debug.Log($"敵生成したよ{_enemy}生成もと:{gameObject}");
    }



    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<MovePlayerKey>() != null)
        {
            Debug.Log("センサー内にいるよ2");
            _players.Add(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<MovePlayerKey>() != null)
        {
            Debug.Log("センサー内にいないよ2");
            _players.Remove(other.transform);
        }
    }

    public List<Transform> GetPlayers()
    {
        return _players;
    }
}
