using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectPool : MonoBehaviour
{
    public static NetworkObjectPool Instance { get; private set; }

    private Dictionary<NetworkObject, Queue<NetworkObject>> pool = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    // プレハブ別にプールを作成（一度だけ）
    private void EnsurePoolExists(NetworkObject prefab)
    {
        if (!pool.ContainsKey(prefab))
        {
            pool[prefab] = new Queue<NetworkObject>();
        }
    }

    // ------------- Spawn（プールから取得 or 新規生成）-------------
    public NetworkObject Get(NetworkObject prefab, Vector3 pos, Quaternion rot)
    {
        EnsurePoolExists(prefab);

        NetworkObject obj;

        if (pool[prefab].Count > 0)
        {
            obj = pool[prefab].Dequeue();
        }
        else
        {
            obj = Instantiate(prefab,pos,rot);
        }

        obj.transform.SetPositionAndRotation(pos, rot);
        obj.gameObject.SetActive(true);

        return obj;
    }

    // ------------- Despawn（破棄せずプールに戻す）-------------
    public void Return(NetworkObject prefab, NetworkObject obj)
    {
        obj.Despawn(false);  // Destroyしない！
        obj.gameObject.SetActive(false);

        EnsurePoolExists(prefab);
        pool[prefab].Enqueue(obj);
    }
}
