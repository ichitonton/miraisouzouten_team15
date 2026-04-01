using UnityEngine;

public class OtukimiDangoEvent : EventBasic
{
    public override void Event()
    {
        isEvent = true;
        Debug.Log("お月見団子のイベント");
        NetworkObjectSpawner.Instance.RequestSpawnObject(_eventName, Vector3.zero, Quaternion.identity, NetworkObjectSpawner.OwnerMode.Host);
    }

    public override void Destroy()
    {
        
    }
}
