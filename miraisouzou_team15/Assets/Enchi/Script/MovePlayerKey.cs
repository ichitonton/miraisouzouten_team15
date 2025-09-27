using UnityEngine;

public class MovePlayerKey : MonoBehaviour
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

        if (Input.GetKey(KeyCode.W))
        {
            _moveVector.z = _moveSpeed;
            // Time.deltaTime フレームレートに関わらず一定の速度でオブジェクトを移動させることができる
            // 要はフレームレートに依存させない仕組み
        }
        if (Input.GetKey(KeyCode.A))
        {
            _moveVector.x = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.S))
        {
            _moveVector.z = -_moveSpeed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            _moveVector.x = _moveSpeed;
        }
        _rb.linearVelocity = _moveVector;
    }
}
