using System;
using Unity.Netcode;
using UnityEngine;

public class Cannon : NetworkBehaviour
{
    [SerializeField] GameObject _bullet;
    [SerializeField] Transform _bulletTransform;
    [SerializeField] float _shotDelay = 2.0f;

    Transform _kari;
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            GenerateBulletServerRpc();
        }
    }


    [ServerRpc(RequireOwnership = false)]
    void GenerateBulletServerRpc()
    {
        if (!IsServer) return; // Å© Ç±ÇÍÇ™ïKê{
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        NetworkObject obj = _ObjectPool.Get(_bullet.GetComponent<NetworkObject>(), transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_bullet.GetComponent<NetworkObject>());

        _kari = obj.transform;
        _kari.position = _bulletTransform.position;
        _kari.rotation = _bulletTransform.rotation;

        Invoke("GenerateBulletServerRpc", _shotDelay);
    }
}
