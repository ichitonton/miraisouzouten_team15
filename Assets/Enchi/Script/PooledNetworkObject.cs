using Unity.Netcode;
using UnityEngine;

public class PooledNetworkObject : NetworkBehaviour
{
    NetworkObject prefab;

    public override void OnNetworkSpawn()
    {
        SetPrefab(NetworkObject);
    }

    public void SetPrefab(NetworkObject pf)
    {
        prefab = pf;
    }

    public void Init(Vector3 dir)
    {
        GetComponent<Rigidbody>().linearVelocity = dir;
    }

    //再利用可能の状態にする(ゲーム上からは消える)
    public void DestroySelf()
    {
        //リクエスト
        DestroySelfServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void DestroySelfServerRpc()
    {
        NetworkObjectPool.Instance.Return(prefab, NetworkObject);
    }
}
