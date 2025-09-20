using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WiiGetCondition : MonoBehaviour
{
    [SerializeField] WiiRemoteInput _wiiInput;
    [SerializeField]TextMeshProUGUI _text;
    [SerializeField] int _remoteNum = 1;

    // Start is called before the first frame update
    void Start()
    {
        _remoteNum -= 1;
    }

    // Update is called once per frame
    void Update()
    {
        _text.SetText(
            "Accel[X]" + _wiiInput.GetAccel(_remoteNum)[0] + "\n" +
            "Accel[Y]" + _wiiInput.GetAccel(_remoteNum)[1] + "\n" +
            "Accel[Z]" + _wiiInput.GetAccel(_remoteNum)[2] + "\n" +
            "pointing" + _wiiInput.GetIR(_remoteNum)[0] + "\n" + 
            "pointing" + _wiiInput.GetIR(_remoteNum)[1] + "\n" + 
            "nunchuk[X]" + _wiiInput.GetStick(_remoteNum)[0] + "\n" + 
            "nunchuk[Y]" + _wiiInput.GetStick(_remoteNum)[1] + "\n");
    }
}
