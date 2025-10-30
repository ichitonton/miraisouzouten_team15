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
    private float _moveSpeedInitial;

    [SerializeField] CanJump _FootCollider;



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
        if (!_toGetPunch)
        {
            Move();
            Jump();
        }
        Punch();
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

}
