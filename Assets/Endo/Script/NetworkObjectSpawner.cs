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
        //ネットワークオブジェクトのPrefabの一覧から探すよ
        var list = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;

        foreach (var entry in list)
        {
            if (entry.SourcePrefabGlobalObjectIdHash == prefabHash)
            {
                return entry.Prefab;
            }
        }

        return null;
    }


    public void RequestSpawnObject(GameObject instance,Vector3 transform,Quaternion rotation)
    {

        // prefabInstance → 対応する元Prefabを取得
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);

        if (prefab == null)
        {
            Debug.LogError("このオブジェクトはPrefabインスタンスではありません。");
            return;
        }

        // Prefab の GlobalObjectId → Hash に変換
        var gid = GlobalObjectId.GetGlobalObjectIdSlow(prefab);
        uint hash = (uint)gid.GetHashCode();

        Debug.Log($"[Client] Prefab Spawn リクエスト送信 Hash={hash}");

        // Host にリクエスト送信
        RequestSpawnObjectServerRpc(hash, NetworkManager.Singleton.LocalClientId, transform,rotation);

    }
    
    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnObjectServerRpc(uint prefabHash, ulong clientId,Vector3 transform,Quaternion rotation)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] Clientで誤実行されたためスキップ");
            return;
        }

        Debug.Log($"[Host] Client {clientId} から Spawn 要求を受信 (hash={prefabHash})");

        // Prefab を取得
        var prefab = GetPrefabFromHash(prefabHash);

        if (prefab == null)
        {
            Debug.LogError($"[Host] hash={prefabHash} に一致するPrefabが見つかりません");
            return;
        }

        // 生成する
        GameObject obj = Instantiate(prefab, transform, rotation);

        // ネットワーク登録
        var netObj = obj.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(clientId);

        return;
    }

}
