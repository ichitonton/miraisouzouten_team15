using UnityEngine;

public class CanJump : MonoBehaviour
{
    public bool _canJump = false;
    void OnTriggerStay(Collider collider)
    {
        _canJump = false;
        if (collider.gameObject.tag == "Field")
        {
            _canJump = true;
        }
    }
    void OnTriggerExit(Collider collider)
    {
        if (collider.gameObject.tag == "Field")
        {
            _canJump = false;
        }
    }

    public bool GetCanJump()
    {
        return _canJump;
    }

}
