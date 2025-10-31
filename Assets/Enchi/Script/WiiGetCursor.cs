using UnityEngine;
using UnityEngine.UI;

public class WiiGetCursor : MonoBehaviour
{
    [SerializeField] WiiRemoteInput _wiiInput;
    RectTransform _transform;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _transform = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        _transform.position = _wiiInput.GetIR(0);
    }
}
