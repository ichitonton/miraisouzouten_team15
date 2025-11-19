using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class NetworkObjectSpawner : NetworkBehaviour
{
    //オブジェクト生成専用のやつ
    public static NetworkObjectSpawner Instance; // 唯一のインスタンス
    [Header("スポーンさせるオブジェクトの一覧登録用ScriptableObject")]
    [SerializeField] private NetworkPrefabDatabase _prefabDatabase;


    public enum OwnerMode
    {
        Host,
        Client
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        // すでに存在していたら自分を消す
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 自分を唯一のインスタンスとして登録
        Instance = this;

        // シーン切り替えで消えないようにする
        DontDestroyOnLoad(gameObject);
    }


    public void RequestSpawnObject(string prefabID,Transform transform,OwnerMode mode)
    {

        RequestSpawnObjectServerRpc(prefabID,NetworkManager.Singleton.LocalClientId,transform.position,transform.rotation,mode);
    }

    public void RequestSpawnObject(string prefabID, Vector3 position,Quaternion rotation, OwnerMode mode)
    {

        RequestSpawnObjectServerRpc(prefabID, NetworkManager.Singleton.LocalClientId, position, rotation, mode);
    }

    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnObjectServerRpc(string prefabID, ulong clientId, Vector3 position,Quaternion rotation,OwnerMode mode)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] Clientで誤実行されたためスキップ");
            return;
        }


        //登録しておいたオブジェクトデータベースの中から該当のオブジェクトを探す
        var prefab = _prefabDatabase.GetPrefab(prefabID);
        if (prefab == null)
        {
            Debug.LogError($"Prefab ID '{prefabID}' が見つからない！");
            return;
        }

        var obj = Instantiate(prefab,position,rotation);

        if (mode == OwnerMode.Host)
        {
            //このオブジェクトの正がHostってこと
            //処理はHostでやってClientに同期するよ
            obj.GetComponent<NetworkObject>().Spawn();
        }
        else if (mode == OwnerMode.Client)
        {
            //このオブジェクトの正がClientってこと
            //処理はClientでやってそれをHostに同期してもらうよ
            obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        }

        return;
    }

}
