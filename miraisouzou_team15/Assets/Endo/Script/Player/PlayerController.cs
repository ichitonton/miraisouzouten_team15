using UnityEngine;
using Unity.Netcode;

public class PlayerController : MonoBehaviour
{

    [SerializeField]private float moveSpeed = 5f;
    private Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 input = Vector3.zero;
        
        //if (!IsOwner) return;

        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;

        //Debug.Log("ÇÕÇµÇÈÅ[");

        input = input.normalized * moveSpeed;

        
        rb.linearVelocity = new Vector3(input.x, rb.linearVelocity.y, input.z);

    }
}
