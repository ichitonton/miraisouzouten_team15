using UnityEngine;
using System.Collections.Generic;

public class playerSpawner : MonoBehaviour
{
    [SerializeField] GameObject _player1;
    [SerializeField] GameObject _player2;
    [SerializeField] GameObject _joint;
    [SerializeField] int _jointCount;

    private List<GameObject> _joints;
    private GameObject _a;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject _kari;
        Vector3 spawnRotation = new Vector3(0, 0, 0);
        for (int i = 0; i < _jointCount; i++)
        {
            _kari = Instantiate(_joint, this.transform.position, this.transform.rotation, this.transform);

            if (i == 0)
            {
                _a = _player1;
            }

            _kari.GetComponent<ConfigurableJoint>().connectedBody = _a.GetComponent<Rigidbody>();

            _a = _kari;
        }

        _player2.GetComponent<ConfigurableJoint>().connectedBody = _a.GetComponent<Rigidbody>();
    }

}
