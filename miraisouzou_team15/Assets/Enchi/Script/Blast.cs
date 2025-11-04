using UnityEngine;

public class Blast : MonoBehaviour
{
    [SerializeField] float _lifeTime = 0.1f;
    [SerializeField] float _impactForce = 10.0f;

    Rigidbody _rigidbody;
    float _boneTime = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        //Invoke("ActiveFalse", _lifeTime);
        _boneTime = 0.0f;
    }
    void OnEnable()
    {
        _boneTime = 0.0f;
    }
    // Update is called once per frame
    void Update()
    {
        _boneTime += Time.deltaTime;

        if (_boneTime >= _lifeTime)
        {
            ActiveFalse();
        }

    }

    void ActiveFalse()
    {
        this.gameObject.SetActive(false);
    }

    //Ç‘Ç¬Ç©Ç¡ÇΩÇ∆Ç´ÇÃèàóù
    void OnCollisionEnter(Collision other)
    {
                Debug.Log("Blast Hit : " + other.transform.name);
        if (other.transform.GetComponent<Rigidbody>() != null)
        {
            if (other.transform.GetComponent<MovePlayerKey>() != null)
            {
                other.transform.GetComponent<MovePlayerKey>().Stun(2.0f);
            }

            Vector3 _distance = other.transform.position - transform.position;

            _distance.Normalize();
            _distance.y = 0.0f;

            other.transform.GetComponent<Rigidbody>().AddForce((_distance + Vector3.up) * _impactForce, ForceMode.Impulse);


        }
    }
}
