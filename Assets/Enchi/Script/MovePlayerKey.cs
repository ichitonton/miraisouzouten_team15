using NUnit.Framework.Constraints;
using UnityEngine;

public class MovePlayerKey : MonoBehaviour
{
    [SerializeField] float _moveSpeed = 7.0f;
    [SerializeField] float _jumpForce = 7.0f;
    [SerializeField] KeyCode _up;
    [SerializeField] KeyCode _down;
    [SerializeField] KeyCode _left;
    [SerializeField] KeyCode _right;
    [SerializeField] KeyCode _jump;
    [SerializeField] KeyCode _punch;
    [SerializeField] KeyCode _useItem;

    [SerializeField] float _punchDuration = 0.5f;
    [SerializeField] float _punchDelay = 0.5f;
    [SerializeField] GameObject _punchObj;
    [SerializeField] float _toGetPunchTime = 0.5f;
    [SerializeField] int _MaxHp = 100;
    [SerializeField] int _PunchDamage = 20;
    [SerializeField] GameObject _item;


    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;
    private bool _canNotInputKey = false;
    private int _currentHp;
    private float _moveSpeedInitial;
    private bool _canPunch = true;
    private Item _haveItem = Item.Bomb;

    [SerializeField] CanJump _FootCollider;

    enum Item
    {
        None,
        Bomb
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
        if (_haveItem != Item.None)
        {
            UseItem();
        }
    }

    public void SetMoveSpeedDamp(float DampValue)
    {
        _moveSpeed = _moveSpeedInitial * DampValue;
    }

    public void SetMoveSpeedInitial()
    {
        _moveSpeed = _moveSpeedInitial;
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
    void PunchActiveFalse()
    {
        _punchObj.SetActive(false);
    }
    void SetPunchReset()
    {
        _canPunch = true;
    }
    public void ToGetPunch(int damage)
    {
        _canNotInputKey = true;
        Invoke(nameof(CanNotInputKeyFalse), _toGetPunchTime);
        AddDamage(damage);
    }
    public void SetCanNotInputKey(float delay)
    {
        Debug.Log("受けうつけないお");
        _canNotInputKey = true;

        Invoke(nameof(CanNotInputKeyFalse), delay);
    }
    void CanNotInputKeyFalse()
    {
        _canNotInputKey = false;
    }

    void AddDamage(int damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Debug.Log(this.gameObject.name + " is dead.");
        }
    }
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
    public int GetPunchDamage()
    {
        return _PunchDamage;
    }

    void UseItem()
    {
        if (Input.GetKeyDown(_useItem))
        {
            bool _isChild = false;

            //for (int i = 0; i < transform.childCount; i++)
            //{
            //    //非アクティブの子オブジェクト検索
            //    Transform _kari = transform.parent.GetChild(i);
            //    if (_kari.gameObject.GetComponent<Item>() != null &&
            //        !_kari.gameObject.activeSelf)
            //    {
            //        _kari.gameObject.SetActive(true);
            //        _kari.position = transform.position;
            //        _kari.rotation = transform.rotation;

            //        _isChild = true;
            //        break;
            //    }
            //}

            //子オブジェクトが足りなければ新規作成
            if (!_isChild)
            {
                Instantiate(_item, transform.position + transform.forward * 1.0f, transform.rotation, transform.parent).
                    GetComponent<Rigidbody>().AddForce((transform.forward + Vector3.up) * 4.0f,ForceMode.Impulse);
            }
        }
    }


}
