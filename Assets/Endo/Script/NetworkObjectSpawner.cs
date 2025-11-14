using Unity.Netcode;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkObjectSpawner : NetworkBehaviour
{
    //オブジェクト生成専用のやつ

    public static NetworkObjectSpawner Instance; // ★ 唯一のインスタンス
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


    private GameObject GetPrefabFromHash(uint prefabHash)
    {
        var prefabs = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;

        foreach (var entry in prefabs)
        {
            if (entry.SourcePrefabGlobalObjectIdHash == prefabHash)
            {
                return entry.Prefab;
            }
        }

        Debug.LogError($"[Netcode] Prefab with hash {prefabHash} not found in NetworkConfig.Prefabs");
        return null;
    }


    public void ObjectSpawn(GameObject instance)
    {

        // インスタンスがどのPrefabから作られたか取得
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);

        if (prefab == null)
        {
            Debug.LogWarning("このオブジェクトはPrefabインスタンスではありません。");
            return;
        }

        // Prefab の GlobalObjectId を取得 → ハッシュ化
        var gid = GlobalObjectId.GetGlobalObjectIdSlow(prefab);
        
        RequestObjectSpawn(gid.GetHashCode(),NetworkManager.Singleton.LocalClientId);

    }
    
    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestObjectSpawn(int prefabHash, ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] Clientで誤実行されたためスキップ");
            return;
        }

        GameObject instance = GetPrefabFromHash((uint)prefabHash);
        
        GameObject obj = Instantiate(instance);
        obj.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

        return;
    }

}
