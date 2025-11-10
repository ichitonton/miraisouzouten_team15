using NUnit.Framework.Constraints;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class MovePlayerKey : MonoBehaviour
{
    [SerializeField] KeyCode _up;
    [SerializeField] KeyCode _down;
    [SerializeField] KeyCode _left;
    [SerializeField] KeyCode _right;
    [SerializeField] KeyCode _jump;
    [SerializeField] KeyCode _punch;
    [SerializeField] KeyCode _useItem;

    [SerializeField] float _moveSpeed = 7.0f;
    [SerializeField] float _jumpForce = 7.0f;

    [SerializeField] float _punchDuration = 0.5f;
    [SerializeField] float _punchDelay = 0.5f;
    [SerializeField] GameObject _punchObj;
    [SerializeField] float _toGetPunchTime = 0.5f;
    [SerializeField] int _MaxHp = 100;
    [SerializeField] int _punchDamage = 20;
    [SerializeField] float _punchForce = 10.0f;
    [SerializeField] float _stunTime = 1.0f;//パンチした時のスタン時間
    [SerializeField] GameObject _itemObj; // 投げるオブジェクト
    [SerializeField] Transform _target;
    [SerializeField] float _itemFlightTime = 2.0f; // 投げるオブジェクトがターゲットに到達するまでの時間


    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;
    private bool _canNotInputKey = false;
    private int _currentHp;
    private float _moveSpeedInitial;
    private bool _canPunch = true;
    private ItemType _haveItem = ItemType.Bomb;

    [SerializeField] CanJump _FootCollider;

    public enum ItemType
    {
        None,
        Bomb,
        Max
    }


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;
        _moveSpeedInitial = _moveSpeed;

    }

    // Update is called once per frame
    void Update()
    {
        if (!_canNotInputKey)
        {
            Move();
            Jump();
            Punch();
        }
        if (_haveItem != ItemType.None)
        {
            UseItem();
        }
    }

    //
    //ゲッター
    //
    public int GetPunchDamage()
    {
        return _punchDamage;
    }
    public float GetPunchForce()
    {
        return _punchForce;
    }
    public float GetStunTime()
    {
        return _stunTime;
    }

    public ItemType GetHaveItem()
    {
        return _haveItem;
    }


    //
    //ステータスいじる関係
    //

    //アイテム入手（アイテム抽選時間）
    public void LotteryHaveItem(float itemLotteryTime)
    {
        _haveItem = ItemType.Max;
        Invoke("SetHaveItem", itemLotteryTime);
    }

    void SetHaveItem()
    {
        _haveItem = (ItemType)Random.Range((int)ItemType.Bomb, (int)ItemType.Max);
    }

    //あべこべ移動速度を逆転させる（何秒後にリセットするか）
    public void MoveSpeedAbekobe(float delay)
    {
        _moveSpeed  *= -1;
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度に倍率をかける（かける倍率）
    public void MoveSpeedChange(float dampValue)
    {
        _moveSpeed = _moveSpeedInitial * dampValue;
    }
    //移動速度に倍率をかける（かける倍率,  何秒後にリセットするか）
    public void MoveSpeedChange(float dampValue, float delay)
    {
        _moveSpeed = _moveSpeedInitial * dampValue;
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度を初期値に戻す
    public void ResetMoveSpeed()
    {
        _moveSpeed = _moveSpeedInitial;
    }
    //パンチオブジェクト非アクティブ化
    void PunchActiveFalse()
    {
        _punchObj.SetActive(false);
    }
    //パンチクールダウンリセット
    void SetPunchReset()
    {
        _canPunch = true;
    }
    //パンチを受ける(ダメージ, パンチをスタン時間)
    public void ToGetPunch(int damage , float stunTime)
    {
        Stun(stunTime);
        AddDamage(damage);
    }

    //スタン（効果時間）
    public void Stun(float delay)
    {
        Debug.Log("受けうつけないお");
        _canNotInputKey = true;

        Invoke(nameof(UnlockStun), delay);
    }
    //スタン解除
    void UnlockStun()
    {
        _canNotInputKey = false;
    }
    //ダメージ（受けるダメージ）
    void AddDamage(int damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Debug.Log(this.gameObject.name + " is dead.");
        }
    }


    //
    //MOVE関係
    //

    void Jump()
    {
        if (_FootCollider.GetCanJump())
        {
            if (Input.GetKeyDown(_jump))
            {
                _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            }
        }
    }
    void Move()
    {

		Vector3 _moveVector = Vector3.zero;

        if (Input.GetKey(_up))
        {
            _moveVector.z += 1;
        }
        if (Input.GetKey(_left))
        {
            _moveVector.x += -1;
        }
        if (Input.GetKey(_down))
        {
            _moveVector.z += -1;
        }
        if (Input.GetKey(_right))
        {
            _moveVector.x += 1;
        }
        _moveVector.Normalize();
		_moveVector *= _moveSpeed;
		_moveVector.y = _rb.linearVelocity.y;


        _moveDir = new Vector3(_moveVector.x, 0, _moveVector.z);

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
        //transform.LookAt(transform.position + new Vector3(_moveVector.x, 0, _moveVector.z));
        _rb.linearVelocity = _moveVector;
    }
    void Punch()
    {
        if (Input.GetKeyDown(_punch) && _canPunch)
        {

            _punchObj.SetActive(true);

            Invoke(nameof(PunchActiveFalse), _punchDuration);

            _canPunch = false;

            Invoke(nameof(SetPunchReset), _punchDelay);
        }
    }

    void UseItem()
    {
        if (Input.GetKeyDown(_useItem))
        {
            bool _isChild = false;
            GameObject _item = null;

            //for (int i = 0; i < transform.childCount; i++)
            //{
            //    //非アクティブの子オブジェクト検索
            //    GameObject _kari = transform.GetChild(i).gameObject;
            //    if (_kari.GetComponent<Item>() != null &&
            //        !_kari.activeSelf)
            //    {
            //        _kari.gameObject.SetActive(true);
            //        _kari.transform.position = transform.position;
            //        _kari.transform.rotation = transform.rotation;

            //        _item = _kari.gameObject;

            //        _isChild = true;
            //        break;
            //    }
            //}

            //子オブジェクトが足りなければ新規作成
            if (!_isChild)
            {
                _item = Instantiate(_itemObj, transform.position + transform.up * 1.5f, transform.rotation);
            }


            if (!_target || !_item) return;
            Rigidbody rb = _item.GetComponent<Rigidbody>();

            // 初速度を計算して付与
            Vector3 velocity = CalculateVelocity(_target.position, _item.transform.position, _itemFlightTime);
            rb.linearVelocity = velocity;
        }

        /// target に time 秒で到達するための初速度を計算
        Vector3 CalculateVelocity(Vector3 target, Vector3 origin, float time)
        {
            Vector3 distance = target - origin;
            Vector3 distanceXZ = new Vector3(distance.x, 0, distance.z);

            float sy = distance.y;
            float sxz = distanceXZ.magnitude;

            Vector3 result = distanceXZ / time; // XZ方向の速度
            result.y = sy / time - 0.5f * Physics.gravity.y * time;

            return result;
        }

    }
}
