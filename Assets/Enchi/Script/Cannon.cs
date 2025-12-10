using System;
using Unity.Netcode;
using UnityEngine;

public class Cannon : NetworkBehaviour
{
    [SerializeField] GameObject _bullet;
    [SerializeField] Transform _bulletTransform;
    [SerializeField] float _shotDelay = 2.0f;

    Transform _kari;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    //        Debug.Log("ってーーーーーーーーーーーー！！");
    //        GenerateBulletServerRpc();
    //    if (IsOwner) // ← これが必須
    //    {
       // }
    }
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
        if (!IsServer) return; // ← これが必須
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        NetworkObject obj = _ObjectPool.Get(_bullet.GetComponent<NetworkObject>(), transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_bullet.GetComponent<NetworkObject>());

        _kari = obj.transform;
        _kari.position = _bulletTransform.position;
        _kari.rotation = _bulletTransform.rotation;
        //_isChild = false;

        //for (int i = 0; i < transform.childCount; i++)
        //{
        //    //非アクティブの子オブジェクト検索
        //    _kari = transform.GetChild(i);
        //    if (_kari.gameObject.GetComponent<CannonBullet>() != null &&
        //        !_kari.gameObject.activeSelf)
        //    {
        //        _kari.gameObject.SetActive(true);
        //        _kari.position = _bulletTransform.position;
        //        _kari.rotation = _bulletTransform.rotation;

        //        _isChild = true;
        //        break;
        //    }
        //}

        ////子オブジェクトが足りなければ新規作成
        //if (!_isChild)
        //{
        //    Instantiate(_bullet, _bulletTransform.position, _bulletTransform.rotation, transform);
        //}

        Invoke("GenerateBulletServerRpc", _shotDelay);
    }




}
