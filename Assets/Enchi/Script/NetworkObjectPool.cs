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

        //DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        ClearAll();
    }

    //  これを呼ぶ：再接続 / リスタート時に必ず全破棄
    public void ClearAll()
    {
        foreach (var kv in pool)
        {
            var q = kv.Value;
            while (q.Count > 0)
            {
                var obj = q.Dequeue();
                if (obj != null) Destroy(obj.gameObject);
            }
        }
        pool.Clear();
        Debug.Log("[NetworkObjectPool] Cleared");
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
        if (prefab == null) return null;

        if (pool.TryGetValue(prefab, out var q))
        {
            while (q.Count > 0)
            {
                var inst = q.Dequeue();

                // Destroy済みは捨てる
                if (inst == null || inst.gameObject == null)
                    continue;

                inst.transform.SetPositionAndRotation(pos, rot);
                inst.gameObject.SetActive(true);
                return inst;
            }
        }

        // 無ければ生成（例）
        var created = Instantiate(prefab, pos, rot);
        return created;
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
