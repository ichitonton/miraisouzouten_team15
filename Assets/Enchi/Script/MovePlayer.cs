using UnityEngine;

public class MovePlayer : MonoBehaviour
{
    [SerializeField]WiiRemoteInput _wiiInput;
    [SerializeField] int _playerNum = 1;
    [SerializeField] float _moveSpeed = 3.0f;

    Vector2Int _Stick;
    Rigidbody _rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _playerNum -= 1;
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }

    void Move()
    {
        Vector3 _moveVector = Vector3.zero;

        _Stick = _wiiInput.GetStick(_playerNum);
        _moveVector.x = (float)_Stick.x / 100.0f;
        _moveVector.z = (float)_Stick.y / 100.0f;

        _moveVector *= _moveSpeed;

        _rb.linearVelocity = _moveVector; 
    }
}
