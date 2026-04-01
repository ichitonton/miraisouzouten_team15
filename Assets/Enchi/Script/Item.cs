using Unity.Mathematics;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class Item : NetworkBehaviour
{
    [SerializeField] GameObject _blast;
    [SerializeField] float _blastTimer = 1.0f;
    bool _isTimerOn = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        if(!IsServer)return;
        Invoke("IsTmerOn", 0.5f);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        _isTimerOn = false;
    }
    void IsTmerOn()
    {
        _isTimerOn = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ThrowServerRpc(Vector3 velocity)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.linearVelocity = velocity;
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        Invoke("SetColTriggerServerRpc", 0.5f);
    }

    [ServerRpc(RequireOwnership = false)]
    void SetColTriggerServerRpc()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = false;
    }

    [ServerRpc(RequireOwnership = false)]
    void BlastGenerateServerRpc()
    {
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        if (_ObjectPool == null)
        {
            Debug.LogError("NetworkObjectPool: prefab is NULL");
            return;
        }
        NetworkObject obj = _ObjectPool.Get(_blast.GetComponent<NetworkObject>(), transform.position, quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_blast.GetComponent<NetworkObject>());
        GetComponent<PooledNetworkObject>().DestroySelf();
    }

    void OnCollisionEnter(Collision other)
    {
        if (!IsServer) return; // Å© Ç±ÇÍÇ™ïKê{
        if (!_isTimerOn)
        {
            if (other.gameObject.tag == "Field" || other.gameObject.GetComponent<Collider>().isTrigger == false)
            {
                BlastGenerateServerRpc();
                //Invoke("BlastGenerateServerRpc", _blastTimer);
                _isTimerOn = true;
            }
        }

    }
}
