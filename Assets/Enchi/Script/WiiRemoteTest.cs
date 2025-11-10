using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WiiRemoteTest : MonoBehaviour
{
    [SerializeField] WiiRemoteInput _wiiInput;

    [SerializeField] Transform _wiiRemote;
    // Start is called before the first frame update
    void Start()
    {
        if (_wiiInput != null)
        {

        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_wiiInput.GetButtonA(0))
        {

        }

        // ピッチ・ロール計算
        Vector3 _angles = Vector3.zero;

        float pitch =
            Mathf.Atan2(
                _wiiInput.GetAccel(0).x,
                Mathf.Sqrt(
                    _wiiInput.GetAccel(0).y * _wiiInput.GetAccel(0).y +
                _wiiInput.GetAccel(0).z * _wiiInput.GetAccel(0).z)) * Mathf.Rad2Deg;
        float roll =
            Mathf.Atan2(
                _wiiInput.GetAccel(0).y, _wiiInput.GetAccel(0).z) * Mathf.Rad2Deg;

        _angles.x = pitch;
        _angles.z = roll;
        _wiiRemote.eulerAngles = _angles;
    }
}
