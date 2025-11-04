using UnityEngine;

public class CannonBullet : MonoBehaviour
{
    [SerializeField] GameObject _blast;
    [SerializeField] float _speed = 10.0f;
    [SerializeField] float _lifeTime = 5.0f;

    Rigidbody _rigidbody;
    float _boneTime = 0.0f;
    bool _isChild = false;
    Transform _kari;    
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
        _rigidbody.linearVelocity = transform.forward * _speed;

        if (_boneTime >= _lifeTime)
        {
            ActiveFalse();
        }

    }

    void ActiveFalse()
    {
        this.gameObject.SetActive(false);
    }

    //ぶつかったときの処理
    void OnTriggerEnter(Collider other)
    {
        if (other != this.transform.parent && other.GetComponent<Rigidbody>() != null)
        {
            _isChild = false;

            for (int i = 0; i < transform.childCount; i++)
            {
                //非アクティブの子オブジェクト検索
                _kari = transform.parent.GetChild(i);
                if (_kari.gameObject.GetComponent<Blast>() != null &&
                    !_kari.gameObject.activeSelf)
                {
                    _kari.gameObject.SetActive(true);
                    _kari.position = transform.position;
                    _kari.rotation = transform.rotation;

                    _isChild = true;
                    break;
                }
            }

            //子オブジェクトが足りなければ新規作成
            if (!_isChild)
            {
                Instantiate(_blast, transform.position, transform.rotation, transform.parent);
            }
            ActiveFalse();
        }
    }
}
