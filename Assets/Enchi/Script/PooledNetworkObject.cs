using Unity.Netcode;
using UnityEngine;

public class PooledNetworkObject : NetworkBehaviour
{
    NetworkObject prefab;

    public void SetPrefab(NetworkObject pf)
    {
        prefab = pf;
    }

    public void Init(Vector3 dir)
    {
        GetComponent<Rigidbody>().linearVelocity = dir;
    }

    //private void OnCollisionEnter(Collision col)
    //{
    //    DestroySelf();
    //}

    public void DestroySelf()
    {
        NetworkObjectPool.Instance.Return(prefab, NetworkObject);
    }
}
