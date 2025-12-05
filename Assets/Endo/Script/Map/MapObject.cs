using UnityEngine;

[DisallowMultipleComponent]
public class MapObject : MonoBehaviour
{

    [Header("Map Combine Settings")]
    [Tooltip("true の場合、このオブジェクトはマップ用に他のメッシュと結合される")]
    [SerializeField] private bool _combineToStatic = true;

    public bool CombineToStatic => _combineToStatic;
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
