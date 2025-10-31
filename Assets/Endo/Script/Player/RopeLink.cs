using UnityEngine;

public class RopeLink : MonoBehaviour
{
    [Header("ジョイント相手")]
    [SerializeField] private GameObject _connectedObject;

    private float _maxDistance;

    private Rigidbody _connectedRb;
    private Rigidbody _rb;
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _connectedRb = _connectedObject.GetComponent<Rigidbody>();

        _maxDistance = GetComponent<SpringJoint>().maxDistance;

    }

    // Update is called once per frame
    void LateUpdate()
    {

        if (_connectedRb == null) return;

        Vector3 dir = _connectedRb.position - _rb.position;

        //ベクトルの長さ
        float dist = dir.magnitude;

        if(dist > _maxDistance)
        {
            //離れている差分を計算
            Vector3 correction = dir.normalized * (dist - _maxDistance);

            _rb.position += correction * 0.5f;
            _connectedRb.position -= correction * 0.5f;

        }


    }
}
