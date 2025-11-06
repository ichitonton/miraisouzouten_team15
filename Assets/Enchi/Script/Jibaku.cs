using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _isTimerOn = false;
    }

    // Update is called once per frame
    void Update()
    {
        //プレイヤー追尾
        _players = _sensour.GetPlayers();

        //最も近いプレイヤーを探す
        if (_players != null && _players.Count > 0)
        {
            _distance = Vector3.zero;
            _distanceSub = Vector3.zero;

            for (int i = 0; i < _players.Count; i++)
            {
                _distanceSub = _players[i].position - transform.position;

                if (_distance == Vector3.zero)
                {
                    _distance = _distanceSub;
                }
                else if (_distance.sqrMagnitude > _distanceSub.sqrMagnitude)
                {
                    _distance = _distanceSub;
                }
            }


            _moveDir = new Vector3(_distance.x, 0, _distance.z);
            _moveDir.Normalize();

            if (_moveDir.sqrMagnitude > 0.01f)
            {
                _lastMoveDir = _moveDir;

                Quaternion targetRotation = Quaternion.LookRotation(_moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10.0f
                );
            }
            else if (_lastMoveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_lastMoveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10.0f
                );

            }
            if (_rigidbody.linearVelocity.y > 0)
            {
                _moveDir.y = 0.0f;
            }
            else
            {
                _moveDir.y = _rigidbody.linearVelocity.y * 0.5f;
            }
                _rigidbody.linearVelocity = _moveDir * _moveSpeed;
        }
    }

    void ActiveFalse()
    {
        this.gameObject.SetActive(false);
    }

    void BlastGenerate()
    {
        if (gameObject.activeSelf)
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
            ActiveFalse();
        }
    }
}
