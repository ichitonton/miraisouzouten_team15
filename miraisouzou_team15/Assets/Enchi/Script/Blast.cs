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
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Rigidbody>() != null)
        {
            Debug.Log("Blast Hit : " + other.name);
            if (other.GetComponent<MovePlayerKey>() != null)
            {
                other.GetComponent<MovePlayerKey>().SetCanNotInputKey(0.5f);
            }

            Vector3 _distance = other.transform.position - transform.position;

            _distance.Normalize();
            _distance.y = 0.0f;

            other.GetComponent<Rigidbody>().AddForce((_distance + Vector3.up) * _impactForce, ForceMode.Impulse);


        }
    }
}
