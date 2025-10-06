using UnityEngine;

public class freezepos : MonoBehaviour
{
    private Rigidbody _rb;
    private bool _isFreeze = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (!_isFreeze)
            {
                _rb.constraints |= RigidbodyConstraints.FreezePosition;
                _rb.constraints |= RigidbodyConstraints.FreezeRotation;

                _isFreeze = true;
            }
            else if(_isFreeze)
            {
                _rb.constraints &= ~RigidbodyConstraints.FreezePosition;
                _rb.constraints &= ~RigidbodyConstraints.FreezeRotation;


                _isFreeze = false;
            }

        }
    }
}
