using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class NetworkEffectSpawner : NetworkBehaviour
{

    public static NetworkEffectSpawner Instance { get; private set; }

    [Header("エフェクトのデータベース")]
    [SerializeField] private EffectDatabase _effectDatabase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkEffectSpawner] 重複インスタンスがあったので削除しました");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// どこからでも呼べる「エフェクトを再生したい」窓口
    /// </summary>
    public void PlayEffect(int effectId, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            // オフライン時 or ネットワーク前でも一応動くようにしておく
            SpawnEffectLocal(effectId, position, rotation);
            return;
        }

        // Host / Server 側ならそのまま全クライアントへ
        if (IsServer)
        {
            PlayEffectClientRpc(effectId, position, rotation);
        }
        else
        {
            // Client → Host にリクエスト
            RequestPlayEffectServerRpc(effectId, position, rotation);
        }
    }

    public void PlayEffect(string effectKey, Vector3 position, Quaternion rotation)
    {
        int effectId = _effectDatabase.GetEffectId(effectKey);
        if (effectId < 0) return;

        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            // オフライン時 or ネットワーク前でも一応動くようにしておく
            SpawnEffectLocal(effectId, position, rotation);
            return;
        }

        // Host / Server 側ならそのまま全クライアントへ
        if (IsServer)
        {
            PlayEffectClientRpc(effectId, position, rotation);
        }
        else
        {
            // Client → Host にリクエスト
            RequestPlayEffectServerRpc(effectId, position, rotation);
        }
    }


    /// <summary>
    /// 実際にエフェクトのPrefabからInstantiateするローカル処理
    /// （Host, Client 共通で使う）
    /// </summary>
    private void SpawnEffectLocal(int effectId, Vector3 position, Quaternion rotation)
    {
        if (_effectDatabase == null)
        {
            Debug.LogError("[NetworkEffectSpawner] EffectDatabase が設定されていません");
            return;
        }

        var prefab = _effectDatabase.GetEffectPrefab(effectId);
        if (prefab == null)
        {
            // データベース側ですでにWarning出してるのでここは静かでもOK
            return;
        }

        GameObject effect = Object.Instantiate(prefab, position, rotation);

        // パーティクルを自動再生して、数秒で消えるようにしておくと楽
        // Destroy(effect, 5f); などをPrefab側の Particle System の "Stop Action" や
        // 別スクリプトで処理してもOK
    }


    // ======== Client → Host へのリクエスト ==========
    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayEffectServerRpc(int effectId, Vector3 position, Quaternion rotation,
        ServerRpcParams rpcParams = default)
    {

        //Hostでしか実行されないようにする
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[RPC] エフェクト生成がClientで誤実行されたためスキップ");
            return;
        }

        // ここで「どのClientから来たか」を見たければ rpcParams.Receive.SenderClientId が使える
        ulong senderId = rpcParams.Receive.SenderClientId;
        // Debug.Log($"[Server] Client({senderId}) から EffectId={effectId} 再生リクエスト");

        // そのまま全クライアントへ
        PlayEffectClientRpc(effectId, position, rotation);
    }

    // ======== Host → 全クライアントへの通知 ==========
    [ClientRpc]
    private void PlayEffectClientRpc(int effectId, Vector3 position, Quaternion rotation,
        ClientRpcParams clientRpcParams = default)
    {
        SpawnEffectLocal(effectId, position, rotation);
    }

}
