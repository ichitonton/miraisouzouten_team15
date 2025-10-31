using UnityEngine;

public class Resporn : MonoBehaviour
{
    [SerializeField] string _respornEreaTag = "Retry_Board";
    [SerializeField] Transform _respornTranse;

    private Vector3 _position;
    private Vector3 _rotation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _position = this.transform.position;
        _rotation = this.transform.eulerAngles;
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == _respornEreaTag)
        {
            Debug.Log("“–‚½‚Á‚½‚¨");
            this.transform.position = _position;
            this.transform.eulerAngles = _rotation;
        }
    }
}
