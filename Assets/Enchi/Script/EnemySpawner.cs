using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] GameObject _enemy;
    GameObject _enemySave;
    [SerializeField] float _respawnDelay = 5.0f;
    List<Transform> _players = new List<Transform>();
    int _areaId = 0;


    Transform _kari;
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
		var m = Regex.Match(name, @"\d+");
		if (m.Success && int.TryParse(m.Value, out int value))
		{
			Debug.Log($"{name} -> {value}");
			GetComponent<NavMeshSurface>().defaultArea = value;
			_areaId = value;
		}
		else
		{
			Debug.Log($"{name} 数字が見つからん");
			GetComponent<NavMeshSurface>().defaultArea = 0; 
            _areaId = 0;
		}
		//int value =  NavMesh.GetAreaFromName(gameObject.name);
		//bool ok = int.TryParse(name, out value);

		//if (ok)
		//{
		//	Debug.Log($"{name} 変換成功");
		//}
		//else
		//{
		//	Debug.Log($"{name} 変換失敗");
		//}
        Invoke("GenerateEnemyServerRpc",3.0f); 
	}

    public int AreaId()
    {
        return _areaId;
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
        NetworkObject obj = _ObjectPool.Get(_enemy.GetComponent<NetworkObject>(), this.transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_enemy.GetComponent<NetworkObject>());
        obj.gameObject.GetComponent<Jibaku>().SetSpawner(GetComponent<NetworkObject>());
        obj.GetComponent<NavMeshAgent>().areaMask = GetComponent<NavMeshSurface>().defaultArea;
		Debug.Log($"敵生成したよ{_enemy.name}生成もと:{gameObject.name}");
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
