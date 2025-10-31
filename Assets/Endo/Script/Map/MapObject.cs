using UnityEngine;

[DisallowMultipleComponent]
public class MapObject : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        MapColorManager.Register(gameObject);
    }

    private void OnDisable()
    {
        MapColorManager.Unregister(gameObject);
    }
}
