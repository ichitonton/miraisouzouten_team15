using NUnit.Framework;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class Jibaku : NetworkBehaviour
{
    [SerializeField] Sensour _sensour;
    [SerializeField] float _moveSpeed = 3.0f;
    [SerializeField] GameObject _blast;
    [SerializeField] float _blastTimerTouchPlayer = 1.0f;
    [SerializeField] float _blastTimerTouchPunch = 2.0f;

    NetworkObject _spawnerObj;
    EnemySpawner _spawner;

    List<Transform> _players;
    Vector3 _distance;
    Vector3 _distanceSub;
    Rigidbody _rigidbody;
    Vector3 _moveDir;
    Vector3 _lastMoveDir;
    bool _isTimerOn = false;

    bool _isChild = false;
    Transform _kari;

    bool _canBomber = false;


    float _animBlend = 0.0f;
    Animator _anim;
    AnimatorStateInfo _animInfo;
    AnimatorStateInfo _animInfoOld;
    bool _hakken;

    NavMeshAgent _agent;

    bool _ouneTime = false;
    State _state = State.Idol;

    enum State
    {
        Idol,
        Hakken1,
        Oikake,
        Death,
        Hakken2,
        Modoru
    }

    public override void OnNetworkSpawn()
    {
        //Rigidbody rb = GetComponent<Rigidbody>();
        //rb.isKinematic = true; // クライアントでは物理演算しない

            _anim = GetComponent<Animator>();
        if (IsServer)
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = false;
            _isTimerOn = false;
            //_anim = GetComponent<Animator>();
            _animInfo = _anim.GetCurrentAnimatorStateInfo(0);
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = _moveSpeed;
            _agent.updateRotation = true;
            _agent.updatePosition = true;
            _hakken = false;

            _state = State.Idol;
            _animBlend = (float)_state;
        }
        else if(IsClient)
        {
            _animInfo = _anim.GetCurrentAnimatorStateInfo(0);
            GetComponent<Rigidbody>().isKinematic = true;
            GetComponent<NavMeshAgent>().enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsServer)
        {
            if (!_agent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                }
                else
                {
                    Debug.LogError("NavMesh が近くに無い！");
                }
            }
            if (_agent == null) return;
            if (!_agent.enabled) return;
            if (!_spawner) return;
            List<Transform> _karis = new List<Transform>();
            Debug.Log($"センサー内のプレイヤー{_spawner.GetPlayers().Count}");
            for (int i = 0; i < _spawner.GetPlayers().Count; i++)
            {
                for (int j = 0; j < _sensour.GetPlayers().Count; j++)
                {
                    if (_spawner.GetPlayers()[i] == _sensour.GetPlayers()[j])
                    {
                        _karis.Add(_spawner.GetPlayers()[i]);
                    }
                }
            }
            _players = _karis;

            _animInfo = _anim.GetCurrentAnimatorStateInfo(0);

            //Debug.Log("すてーと"+_state.ToString());
            if (_state == State.Idol)
            {
                IdolServerRpc();
            }
            else if (_state == State.Hakken1)
            {
                _agent.SetDestination(PlayerPosition());
                HakkenServerRpc();
            }
            else if (_state == State.Oikake)
            {
                _agent.SetDestination(PlayerPosition());
                OikakeServerRpc();
            }
            _animInfoOld = _animInfo;
            AnimBlendClientRpc(_animBlend);
            AnimBlendServerRpc(_animBlend);
        }
    }

    public void SetSpawner(NetworkObject obj)
    {
        _spawnerObj = obj;
        _spawner = obj.GetComponent<EnemySpawner>();
    }
    [ClientRpc]
    void AnimBlendClientRpc(float blend)
    {
        _anim.SetFloat("Blend", blend);
    }

    //アニメーション
    [ServerRpc(RequireOwnership = false)]
    void AnimBlendServerRpc(float blend)
    {
        _anim.SetFloat("Blend", blend);
    }

    Vector3 PlayerPosition()
    {
        if (_players != null && _players.Count > 0)
        {
            Transform nearest = _players[0];
            float minDist = Mathf.Infinity;

            foreach (var p in _players)
            {
                float sqr = (p.position - transform.position).sqrMagnitude;
                if (sqr < minDist)
                {
                    minDist = sqr;
                    nearest = p;
                }
            }

            return nearest.position;
        }
        else
        {
            // センサー範囲にプレイヤーがいない → 初期位置へ戻る
            return _spawnerObj.transform.position;
        }
    }
    void RotateToMoveDirection()
    {
        Vector3 dir = _agent.velocity;  // ← 進んでる方向そのもの
        dir.y = 0;                      // 上下は無視

        if (dir.sqrMagnitude < 0.0001f)
            return; // 止まってる時は回転しない

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * 10.0f  // ← 回転速度（数字を上げれば速く振り向く）
        );
    }

    [ServerRpc(RequireOwnership = false)]
    void IdolServerRpc()
    {
        bool _isRotating = false;
        float t = _animInfo.normalizedTime % 1.0f;

        // 区間に入った瞬間
        if (!_isRotating && t > 0.1f && t < 0.5f)
        {
            _isRotating = true;
        }

        // 区間を抜けたら止める
        if (_isRotating && t >= 0.5f)
        {
            _isRotating = false;
        }

        if (_isRotating)
        {
            Debug.Log("回転中");
            transform.rotation *= Quaternion.Euler(0f, Time.deltaTime * 90f, 0f);
        }

        //センサー内に入ったプレイヤーを検知
        if (_players != null && _players.Count > 0)
        {
            if (Random.Range(0, 2) == 0)
            {
                Debug.Log("発見した！その1");
                _animBlend = (float)State.Hakken1;
            }
            else
            {
                Debug.Log("発見した！その2");
                _animBlend = (float)State.Hakken2;
            }
            _agent.speed = 0.1f;
            _anim.Play(_anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
            _state = State.Hakken1;
        }
    }
    [ServerRpc(RequireOwnership = false)]
    void HakkenServerRpc()
    {
        // Debug.Log(_animInfo.normalizedTime);]
        //アニメーションが一周したら追いかけ状態へ
        if (_animInfo.normalizedTime > 0.9f)
        {
            if (_animBlend == (float)State.Hakken1 || _animBlend == (float)State.Hakken2)
            {
                Debug.Log("追いかけるよ！");
                _state = State.Oikake;
                _animBlend = (float)_state;

                _anim.Play(_anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
                _hakken = false;
            }
        }
        RotateToMoveDirection();
    }

    [ServerRpc(RequireOwnership = false)]
    void OikakeServerRpc()
    {
        Debug.Log("追いかけ中");
        //Debug.Log($"追いかけ中！{PlayerPosition()}");
        //_agent.SetDestination(PlayerPosition());
        _agent.speed = _moveSpeed;

        RotateToMoveDirection();

        if (PlayerPosition() == _spawnerObj.transform.position)
        {

            float dist = Vector3.Distance(transform.position, _spawnerObj.transform.position);
            Debug.Log($"戻る！{dist}");
            // 初期位置にほぼ戻った
            if (dist < 3.0f)
            {
                Debug.Log("戻った！");
                _state = State.Idol;
                _animBlend = (float)_state;
                _agent.speed = 0.0f;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void BlastGenerateServerRpc()
    {
        if (!IsServer) return; // ← これが必須
        NetworkObjectPool _ObjectPool = NetworkObjectPool.Instance;
        NetworkObject obj = _ObjectPool.Get(_blast.GetComponent<NetworkObject>(), transform.position, Quaternion.identity);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_blast.GetComponent<NetworkObject>());
    }
    [ServerRpc(RequireOwnership = false)]
    void DespawnServerRpc()
    {
        BlastGenerateServerRpc();
        _spawnerObj.GetComponent<EnemySpawner>().EnemyIsDead();
        _rigidbody.isKinematic = true;
        GetComponent<PooledNetworkObject>().DestroySelf();
    }


    //ぶつかったときの処理
    void OnCollisionEnter(Collision other)
    {
        if (!IsServer) return;
        if (other.transform.GetComponent<Rigidbody>() != null)
        {
            Invoke("DespawnServerRpc", _blastTimerTouchPlayer);
        }

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.GetComponent<Punch>() != null)
        {
            //ActiveFalse();
            _state = State.Death;
            _animBlend = (float)_state;
            _anim.Play(_anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
            _isTimerOn = false;
            _agent.speed = 0.0f;
            CancelInvoke("DespawnServerRpc");
            Invoke("DespawnServerRpc", _blastTimerTouchPunch);
        }
    }
}
