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

    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;

    [SerializeField] CanJump _FootCollider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Move();
        Jump();
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
        _moveVector.x = 0.0f;
        _moveVector.z = 0.0f;

        if (Input.GetKey(_up))
        {
            _moveVector.z = _moveSpeed;
            // Time.deltaTime フレームレートに関わらず一定の速度でオブジェクトを移動させることができる
            // 要はフレームレートに依存させない仕組み
        }
        if (Input.GetKey(_left))
        {
            _moveVector.x = -_moveSpeed;
        }
        if (Input.GetKey(_down))
        {
            _moveVector.z = -_moveSpeed;
        }
        if (Input.GetKey(_right))
        {
            _moveVector.x = _moveSpeed;
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

}
