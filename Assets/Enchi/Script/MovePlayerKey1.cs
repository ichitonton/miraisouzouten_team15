using UnityEngine;

public class MovePlayerKey1 : MonoBehaviour
{
    [SerializeField] float _moveSpeed = 3.0f;

    Rigidbody _rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }

    void Move()
    {
        Vector3 _moveVector = Vector3.zero;
        _moveVector.x = 0.0f;
        _moveVector.z = 0.0f;

        if (Input.GetKey(KeyCode.I))
        {
            _moveVector.z = _moveSpeed;
        }
        if (Input.GetKey(KeyCode.J))
        {
            _moveVector.x = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.K))
        {
            _moveVector.z = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.L))
        {
            _moveVector.x = _moveSpeed;
        }
        _moveVector.Normalize();
        _moveVector *= _moveSpeed;
        _moveVector.y = _rb.linearVelocity.y;
        _rb.linearVelocity = _moveVector;
    }
}
