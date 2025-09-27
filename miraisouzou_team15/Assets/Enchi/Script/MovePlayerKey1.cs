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
        Vector3 _moveVector = _rb.linearVelocity;
        _moveVector.x = 0.0f;
        _moveVector.z = 0.0f;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            _moveVector.z = _moveSpeed;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _moveVector.x = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            _moveVector.z = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            _moveVector.x = _moveSpeed;
        }
        _rb.linearVelocity = _moveVector;
    }
}
