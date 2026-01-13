using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPadBinding : MonoBehaviour
{
    public int DeviceId { get; private set; } = -1;
    public bool HasPad => DeviceId >= 0;

    public void Bind(Gamepad pad)
    {
        DeviceId = pad != null ? pad.deviceId : -1;
    }

    public void Unbind()
    {
        DeviceId = -1;
    }

    public Gamepad GetPadOrNull()
    {
        if (DeviceId < 0) return null;

        // deviceIdˆê’v‚·‚épad‚ð’T‚·
        foreach (var p in Gamepad.all)
        {
            if (p != null && p.added && p.enabled && p.deviceId == DeviceId)
                return p;
        }
        return null;
    }
}
