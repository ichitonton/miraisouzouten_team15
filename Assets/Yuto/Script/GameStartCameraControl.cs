using UnityEngine;

public class GameStartCameraControl : MonoBehaviour
{
    [SerializeField] private Transform _targetTrans = null;
    [SerializeField] private float _radius = 10f;
    [SerializeField] private float _speed = 1f;
    [SerializeField] private float _height = 5f;

    private float _angle = 0f;

    void Start()
    {
        
    }


    void Update()
    {
        if (_targetTrans == null) return;

        _angle += _speed * Time.deltaTime;

        float x = Mathf.Cos(_angle) * _radius;
        float z = Mathf.Sin(_angle) * _radius;

        transform.localPosition = _targetTrans.position + new Vector3(x, 0, z);

        transform.LookAt(_targetTrans);

    }
}
