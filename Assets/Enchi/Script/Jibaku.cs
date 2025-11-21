using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class Jibaku : MonoBehaviour
{
    [SerializeField] Sensour _sensour;
    [SerializeField] float _moveSpeed = 3.0f;
    [SerializeField] GameObject _blast;
    [SerializeField] float _blastTimer = 1.0f;

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _isTimerOn = false;
        _anim = GetComponent<Animator>();
        _animInfo = _anim.GetCurrentAnimatorStateInfo(0);
        _agent = GetComponent<NavMeshAgent>();
        _agent.updateRotation = false;
        _hakken = false;
    }

    // Update is called once per frame
    void Update()
    {
        _players = _sensour.GetPlayers();
        _animInfo = _anim.GetCurrentAnimatorStateInfo(0);

        if (_state == State.Idol)
        {
            Idol();
        }
        else if (_state == State.Hakken1)
        {
            Hakken();
        }
        else if (_state == State.Oikake)
        {
            Oikake();
        }
        else if (_state == State.Death)
        {
            Death();
        }





        _anim.SetFloat("Blend", _animBlend);

        _animInfoOld = _animInfo;
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
            return transform.parent.position;
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

    void Idol()
    {
        //まじでごり押しプログラミング
        if (0.5f > _animInfo.normalizedTime % 1.0f && _animInfo.normalizedTime % 1.0f > 0.1f)
        {
            //Debug.Log("回る");
            // Y軸回転を直接変更するには、rotationの値を取得・修正して再代入する必要があります
            Quaternion rot = _rigidbody.rotation;
            Vector3 euler = rot.eulerAngles;
            euler.y += 3.0f;
            transform.rotation = Quaternion.Euler(euler);
        }

        //センサー内に入ったプレイヤーを検知
        if (_players != null && _players.Count > 0)
        {
            _state = State.Hakken1;
        }
    }
    void Hakken()
    {
        // Debug.Log(_animInfo.normalizedTime);]
        //アニメーションが一周したら追いかけ状態へ
        if (_animInfo.normalizedTime - _animInfoOld.normalizedTime < 0.0f)
        {
            if (_animBlend == (float)State.Hakken1 || _animBlend == (float)State.Hakken2)
            {
                Debug.Log("追いかけるよ！");
                _state = State.Oikake;
                _animBlend = (float)_state;
                _hakken = false;
            }
            else if (_animBlend == (float)State.Idol)
            {
                Debug.Log("発見した！");
                _agent.SetDestination(PlayerPosition());
                _agent.speed = 0.1f;
                _hakken = true;

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
            }
        }
                RotateToMoveDirection();
    }

    void Oikake()
    {
        _agent.SetDestination(PlayerPosition());
        _agent.speed = _moveSpeed;

        RotateToMoveDirection();
        
        if (PlayerPosition() == transform.parent.position)
        {
            Debug.Log("戻る！");

            float dist = Vector3.Distance(transform.position, transform.parent.position);
            // 初期位置にほぼ戻った
            if (dist < 1.0f)
            {
                Debug.Log("戻った！");
                _state = State.Idol;
                _animBlend = (float)_state;
                _agent.speed = 0.0f;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    void Death()
    {
        if (_animInfo.normalizedTime > 0.8f)
        {
            _isTimerOn = true;
            BlastGenerate();
        }
    }

    void ActiveFalse()
    {
        this.gameObject.SetActive(false);
    }

    void BlastGenerate()
    {
        if (gameObject.activeSelf && _isTimerOn)
        {
            _isChild = false;

            for (int i = 0; i < transform.childCount; i++)
            {
                //非アクティブの子オブジェクト検索
                _kari = transform.parent.GetChild(i);
                if (_kari.gameObject.GetComponent<Blast>() != null &&
                    !_kari.gameObject.activeSelf)
                {
                    _kari.gameObject.SetActive(true);
                    _kari.position = transform.position;
                    _kari.rotation = transform.rotation;

                    _isChild = true;
                    break;
                }
            }

            //子オブジェクトが足りなければ新規作成
            if (!_isChild)
            {
                Instantiate(_blast, transform.position, transform.rotation, transform.parent);
            }
            ActiveFalse();
        }
    }


    //ぶつかったときの処理
    void OnCollisionEnter(Collision other)
    {
        if (other.transform.GetComponent<Rigidbody>() != null)
        {
            if (!_isTimerOn)
            {
                Invoke("BlastGenerate", _blastTimer);
                _isTimerOn = true;
                
            }
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
        }
    }
}
