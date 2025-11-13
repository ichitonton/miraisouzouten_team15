using Unity.Netcode;
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

    // ======== Host側で実行される ==========
    [ServerRpc(RequireOwnership = false)]
    public void ObjectInstantiate(GameObject instantiateObject, ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] Clientで誤実行されたためスキップ");
            return;
        }
        GameObject obj = Instantiate(instantiateObject, Vector3.zero, Quaternion.identity);

        var netObj = obj.GetComponent<NetworkObject>();
        
        //オブジェクトのオーナーを決める
        netObj.SpawnWithOwnership(clientId);

        // ClientRpcの送信先を1クライアントに限定
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId } // ← ここで送信先を指定！
            }
        };

        return;
    }

}
