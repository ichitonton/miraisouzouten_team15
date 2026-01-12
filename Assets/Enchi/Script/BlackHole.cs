using System;
using Unity.Netcode;
using UnityEngine;

public class BlackHole : NetworkBehaviour
{
    [SerializeField] float _lifeTime = 0.1f;
    [SerializeField] float _impactForce = 10.0f;

	//[SerializeField] int _explosionEffectId = 2;

	Rigidbody _rigidbody;
    float _boneTime = 0.0f;
    bool _isAction = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        //Invoke("ActiveFalse", _lifeTime);
        _boneTime = 0.0f;
        _isAction = true;
    }
    public override void OnNetworkSpawn()
    {
        //Debug.Log("Spawn Ç≥ÇÍÇΩÇÊÅI");
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        _boneTime = 0.0f;
        _isAction = true;
        //Invoke("SetColTriggerServerRpc", 0.4f);
    }

    //   void OnEnable()
    //   {
    //       _boneTime = 0.0f;
    //	NetworkEffectSpawner.Instance.PlayEffect(
    //	   _explosionEffectId,
    //	   transform.position,
    //	   Quaternion.identity
    //   );
    //}
    // Update is called once per frame
    void Update()
    {
        _boneTime += Time.deltaTime;

        if (_boneTime >= 5.0f)
        {
            _isAction = false;
        }
        if (_boneTime >= _lifeTime)
        {
            ActiveFalse();
        }

    }

    
    void ActiveFalse()
    {
        GetComponent<PooledNetworkObject>().DestroySelf();
    }

    //Ç‘Ç¬Ç©Ç¡ÇΩÇ∆Ç´ÇÃèàóù
    void OnTriggerStay (Collider other)
    {
        if (!IsServer) return; // Å© Ç±ÇÍÇ™ïKê{
        if (other.GetComponent<JointLiner>() != null) return;
        if (other.transform.GetComponent<Rigidbody>() != null && _isAction)
        {
            if (other.transform.GetComponent<MovePlayerKey>() != null)
            {
				other.transform.GetComponent<MovePlayerKey>().PlayCameraShake();
                if (other.transform.GetComponent<MovePlayerKey>().GetUseStar() == true)
                {
                    return;
                }
            }

			Vector3 _distance = other.transform.position - transform.position;

            _distance.Normalize();
            _distance.y = 0.0f;

            other.transform.GetComponent<Rigidbody>().AddForce((_distance + Vector3.up) * -_impactForce, ForceMode.VelocityChange);


        }
    }
}
