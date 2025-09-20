using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WiiGetCondition : MonoBehaviour
{
    [SerializeField] WiiRemoteInput _wiiInput;
    [SerializeField]TextMeshProUGUI _text;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _text.SetText(
            "Accel[X]" + _wiiInput.GetAccel(0)[0] + "\n" +
            "Accel[Y]" + _wiiInput.GetAccel(0)[1] + "\n" +
            "Accel[Z]" + _wiiInput.GetAccel(0)[2] + "\n" +
            "pointing" + _wiiInput.GetIR(0)[0] + "\n" + 
            "pointing" + _wiiInput.GetIR(0)[1] + "\n" + 
            "nunchuk[X]" + _wiiInput.GetStick(0).x + "\n" + 
            "nunchuk[Y]" + _wiiInput.GetStick(0).y + "\n");
    }
}
