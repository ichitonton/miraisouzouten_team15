using UnityEngine;

public class Cannon : MonoBehaviour
{
    [SerializeField] GameObject _bullet;
    [SerializeField] Transform _bulletTransform;
    [SerializeField] float _shotDelay = 2.0f;

    Transform _kari;
    bool _isChild = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateBullet();
    }

    void GenerateBullet()
    {
        _isChild = false;

        for (int i = 0; i < transform.childCount; i++)
        {
            //非アクティブの子オブジェクト検索
            _kari = transform.GetChild(i);
            if (_kari.gameObject.GetComponent<CannonBullet>() != null &&
                !_kari.gameObject.activeSelf)
            {
                _kari.gameObject.SetActive(true);
                _kari.position = _bulletTransform.position;
                _kari.rotation = _bulletTransform.rotation;

                _isChild = true;
                break;
            }
        }

        //子オブジェクトが足りなければ新規作成
        if (!_isChild)
        {
            Instantiate(_bullet, _bulletTransform.position, _bulletTransform.rotation, transform);
        }

        Invoke("GenerateBullet", _shotDelay);
    }




}
