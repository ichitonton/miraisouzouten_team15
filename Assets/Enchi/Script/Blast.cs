using System;
using Unity.Netcode;
using UnityEngine;

public class Blast : NetworkBehaviour
{
    [SerializeField] float _lifeTime = 0.1f;
    [SerializeField] float _blastTime = 0.5f;
    [SerializeField] float _impactForce = 10.0f;

	//[SerializeField] int _explosionEffectId = 2;

	Rigidbody _rigidbody;
    float _boneTime = 0.0f;
    bool _isBlast = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        //Invoke("ActiveFalse", _lifeTime);
        _boneTime = 0.0f;
    }
    public override void OnNetworkSpawn()
    {
        //爆発のSE再生、全Clientで3D空間で流す
        NetworkSoundManager.Instance.PlaySfx("Explosion", NetworkSoundManager.SoundScope.AllClients, true, transform.position);

        //Debug.Log("Spawn されたよ！");
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        _isBlast = true;
        _boneTime = 0.0f;
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

        if (_boneTime >= _blastTime)
        {
            _isBlast = false;
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

    //ぶつかったときの処理
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // ← これが必須
        if (!_isBlast) return;
        if (other.transform.GetComponent<Rigidbody>() != null)
        {
            if (other.transform.GetComponent<MovePlayerKey>() != null)
            {
                other.transform.GetComponent<MovePlayerKey>().Stun(2.0f);
				other.transform.GetComponent<MovePlayerKey>().PlayCameraShake();
			}

			Vector3 _distance = other.transform.position - transform.position;

            _distance.Normalize();
            _distance.y = 0.0f;

            other.transform.GetComponent<Rigidbody>().AddForce((_distance + Vector3.up) * _impactForce, ForceMode.Impulse);


        }
    }
}
