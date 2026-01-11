using Unity.Netcode;
using UnityEngine;

public class GoldenWagashiEvent : EventBasic
{
    [SerializeField] private Vector3 _targetPos = Vector3.zero;

    public override void Event()
    {
        isEvent = true;
        Debug.Log("金色和菓子のイベント");
        NetworkObjectSpawner.Instance.RequestSpawnObject(_eventName, _targetPos, Quaternion.identity, NetworkObjectSpawner.OwnerMode.Host);
        _eventTime = 30f;
        Destroy();
    }

    public override void Destroy()
    {
        Destroy(gameObject, _eventTime);
    }
}
