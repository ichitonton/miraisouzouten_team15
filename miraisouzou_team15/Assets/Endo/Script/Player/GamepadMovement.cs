using UnityEngine;
using UnityEngine.InputSystem;

public class GamepadMovement : MonoBehaviour
{

    [SerializeField] float _moveSpeed;
    private Vector2 _moveInput;
    private Rigidbody _rb;
    private Gamepad _gamepad;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();

    }
    private void FixedUpdate()
    {
        // 入力（左右: x, 前後: y）をワールドのXZに変換
        Vector3 move = new Vector3(_moveInput.x, 0, _moveInput.y);
        _rb.MovePosition(_rb.position + move * _moveSpeed * Time.fixedDeltaTime);
    }
}
