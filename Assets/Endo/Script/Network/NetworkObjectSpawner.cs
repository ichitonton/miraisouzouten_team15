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
            Debug.LogError($"PooledNetworkObject ID '{prefabID}' が見つからない！");
            return;
        }


        //オブジェクトプールに対応
        //オブジェクトの生成or再利用
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        if (_ObjectPool == null)
        {
            Debug.LogError("NetworkObjectPool: prefab is NULL");
            return;
        }
        NetworkObject obj = _ObjectPool.Get(prefab.GetComponent<NetworkObject>(), position, rotation);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(prefab.GetComponent<NetworkObject>());

        return;
    }

    // ===============================
    // 追加：指定ポジションから一定範囲内にランダム生成
    // ※元の関数は変更しない
    // ===============================

    /// <summary>
    /// centerから半径radius以内のランダム位置にスポーン（3D球内）
    /// </summary>
    public void RequestSpawnObjectRandomInRange(
        string prefabID,
        Vector3 center,
        float radius,
        Quaternion rotation,
        OwnerMode mode)
    {
        RequestSpawnObjectRandomInRangeServerRpc(
            prefabID,
            NetworkManager.Singleton.LocalClientId,
            center,
            radius,
            rotation,
            mode
        );
    }

    /// <summary>
    /// centerから半径radius以内のランダム位置にスポーン（水平円内：Y固定）
    /// </summary>
    public void RequestSpawnObjectRandomInRange2D(
        string prefabID,
        Vector3 center,
        float radius,
        float fixedY,
        Quaternion rotation,
        OwnerMode mode)
    {
        RequestSpawnObjectRandomInRange2DServerRpc(
            prefabID,
            NetworkManager.Singleton.LocalClientId,
            center,
            radius,
            fixedY,
            rotation,
            mode
        );
    }

    /// <summary>
    /// Transform版（中心をTransformで渡す）
    /// </summary>
    public void RequestSpawnObjectRandomInRange(
        string prefabID,
        Transform centerTransform,
        float radius,
        OwnerMode mode)
    {
        RequestSpawnObjectRandomInRange(
            prefabID,
            centerTransform.position,
            radius,
            centerTransform.rotation,
            mode
        );
    }

    private static Vector3 GetRandomPointInSphere(Vector3 center, float radius)
    {
        if (radius < 0f) radius = 0f;
        return center + (Random.insideUnitSphere * radius);
    }

    private static Vector3 GetRandomPointInCircleXZ(Vector3 center, float radius, float fixedY)
    {
        if (radius < 0f) radius = 0f;

        // 半径内一様（円）
        Vector2 v = Random.insideUnitCircle * radius;
        return new Vector3(center.x + v.x, fixedY, center.z + v.y);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnObjectRandomInRangeServerRpc(
        string prefabID,
        ulong clientId,
        Vector3 center,
        float radius,
        Quaternion rotation,
        OwnerMode mode)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        Vector3 pos = GetRandomPointInSphere(center, radius);

        // 元の関数をそのまま使う（＝元の関数は変更しない）
        RequestSpawnObjectServerRpc(prefabID, clientId, pos, rotation, mode);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnObjectRandomInRange2DServerRpc(
        string prefabID,
        ulong clientId,
        Vector3 center,
        float radius,
        float fixedY,
        Quaternion rotation,
        OwnerMode mode)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        Vector3 pos = GetRandomPointInCircleXZ(center, radius, fixedY);

        // 元の関数をそのまま使う（＝元の関数は変更しない）
        RequestSpawnObjectServerRpc(prefabID, clientId, pos, rotation, mode);
    }

}
