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
    [SerializeField] float _punchDuration = 0.5f;
    [SerializeField] GameObject _punchObj;
    [SerializeField] float _toGetPunchTime = 0.5f;
    [SerializeField] int _MaxHp = 100;
    [SerializeField] int _PunchDamage = 20;


    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;
    private bool _toGetPunch = false;
    private int _currentHp;

    [SerializeField] CanJump _FootCollider;

    // 橋本がいじったよ
	[Header("沼（遅くするエリア）")]
	[Tooltip("沼の中での“上限速度”（一定値まで落とす）")]
	[SerializeField, Min(0f)] float _swampSpeed = 2.5f;         // 沼中の最大速度
	[Tooltip("速度切替のなめらかさ（大きいほど早く切替）")]
	[SerializeField, Min(0f)] float _speedChangeRate = 8f;      // 補間スピード
	[Tooltip("沼オブジェクトに付けたタグ名")]
	[SerializeField] string _swampTag = "Swamp";                // タグで判定
	bool _inSwamp = false;                                      // 沼フラグ（重なり無しなのでboolでOK）
	float _currentMaxSpeed;                                     // 実際に使う上限速度（補間用）


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;

		_currentMaxSpeed = _moveSpeed; // 開始時は通常速度
	}

    // Update is called once per frame
    void Update()
    {
        if (!_toGetPunch)
        {
            Move();
            Jump();
        }
        Punch();
    }


    void Punch()
    {
        if (Input.GetKeyDown(_punch))
        {
            _punchObj.SetActive(true);

            Invoke(nameof(PunchActiveFalse), _punchDuration);

        }
    }

    void PunchActiveFalse()
    {
        _punchObj.SetActive(false);
    }

    public void ToGetPunch(int damage)
    {
        _toGetPunch = true;
        Invoke(nameof(ToGetPunchFalse), _toGetPunchTime);
        AddDamage(damage);
    }

    void ToGetPunchFalse()
    {
        _toGetPunch = false;
    }

    void AddDamage(int damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Debug.Log(this.gameObject.name + " is dead.");
            // You can add additional logic here for when the player dies.
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
		// 追加: 状況に応じた“目標最大速度”を決定し、なめらかに切り替え
		float targetMax = _inSwamp ? _swampSpeed : _moveSpeed;
		_currentMaxSpeed = Mathf.Lerp(_currentMaxSpeed, targetMax, Time.deltaTime * _speedChangeRate);

		Vector3 _moveVector = Vector3.zero;
        _moveVector.x = 0.0f;
        _moveVector.z = 0.0f;

        if (Input.GetKey(_up))
        {
            _moveVector.z = _currentMaxSpeed;
        }
        if (Input.GetKey(_left))
        {
            _moveVector.x = -_currentMaxSpeed;
        }
        if (Input.GetKey(_down))
        {
            _moveVector.z = -_currentMaxSpeed;
        }
        if (Input.GetKey(_right))
        {
            _moveVector.x = _currentMaxSpeed;
        }
        _moveVector.Normalize();
		_moveVector *= _currentMaxSpeed;
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


    //沼の出入り感知
	void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag(_swampTag))
		{
			_inSwamp = true; 
		}
	}

	void OnTriggerExit(Collider other)
	{
		if (other.CompareTag(_swampTag))
		{
			_inSwamp = false; 
		}
	}


	public int GetPunchDamage()
    {
        return _PunchDamage;
    }

}
