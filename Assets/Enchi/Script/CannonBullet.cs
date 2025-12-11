using Unity.Netcode;
using UnityEngine;

public class CannonBullet : NetworkBehaviour
{
    [SerializeField] GameObject _blast;
    [SerializeField] float _speed = 10.0f;
    [SerializeField] float _lifeTime = 5.0f;

    Rigidbody _rigidbody;
    float _boneTime = 0.0f;
    bool _isChild = false;
    Transform _kari;    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Blast enabled? " + this.enabled);
        Debug.Log("GameObject active? " + gameObject.activeInHierarchy);
        Debug.Log("CannonBullet Start");
        _rigidbody = GetComponent<Rigidbody>();
        //Invoke("ActiveFalse", _lifeTime);
        _boneTime = 0.0f;
    }
    public override void OnNetworkSpawn()
    {
        //Debug.Log("Spawn Ç≥ÇÍÇΩÇÊÅI");
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        //Invoke("SetColTriggerServerRpc", 0.4f);
    }
    [ServerRpc(RequireOwnership = false)]
    void SetColTriggerServerRpc()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = false;
    }
    void OnEnable()
    {
        _boneTime = 0.0f;
    }
    // Update is called once per frame
    void Update()
    {
        if (IsOwner)
        {
            
        _boneTime += Time.deltaTime;
        MoveBulletServerRpc();
        if (_boneTime >= _lifeTime)
        {
            ActiveFalseServerRpc();
        }
        }

    }
    [ServerRpc(RequireOwnership = false)]
    void MoveBulletServerRpc()
    {
        _rigidbody.linearVelocity = transform.forward * _speed;
    }

    [ServerRpc(RequireOwnership = false)]
    void BlastGenerateServerRpc()
    {
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        NetworkObject obj = _ObjectPool.Get(_blast.GetComponent<NetworkObject>(), transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_blast.GetComponent<NetworkObject>());
        GetComponent<PooledNetworkObject>().DestroySelf();
    }

    [ServerRpc(RequireOwnership = false)]
    void ActiveFalseServerRpc()
    {
        GetComponent<PooledNetworkObject>().DestroySelf();
    }

    //Ç‘Ç¬Ç©Ç¡ÇΩÇ∆Ç´ÇÃèàóù
    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (!IsServer) return; // Å© Ç±ÇÍÇ™ïK
        if (other.GetComponent<Cannon>() != null) return;
        Debug.Log("other Ç≥ÇÍÇΩÇÊÅI");
        BlastGenerateServerRpc();
        //ActiveFalseServerRpc();
    }
}
