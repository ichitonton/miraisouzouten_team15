using UnityEngine;

public class CannonBullet : MonoBehaviour
{
    [SerializeField] float _speed = 10.0f;
    [SerializeField] float _lifeTime = 5.0f;

    Rigidbody _rigidbody;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        Invoke("ActiveFalse", _lifeTime);
    }
    void OnEnable()
    {
        Invoke("ActiveFalse", _lifeTime);
    }
    // Update is called once per frame
    void Update()
    {
        _rigidbody.linearVelocity = transform.forward * _speed;
    }

    void ActiveFalse()
    {
        this.gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other != this.transform.parent && other.GetComponent<Rigidbody>() != null)
        {
            if (other.GetComponent<MovePlayerKey>() != null)
            {
                other.GetComponent<MovePlayerKey>().SetCanNotInputKey(0.5f);
            }

            Vector3 _distance = other.transform.position - transform.position;

            _distance.Normalize();
            _distance.y = 0.0f;

            other.GetComponent<Rigidbody>().AddForce((_distance + Vector3.up * 0.3f) * 10.0f, ForceMode.Impulse);

            this.gameObject.SetActive(false);
        }
    }
}
