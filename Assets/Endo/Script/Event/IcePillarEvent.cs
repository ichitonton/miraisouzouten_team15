using Unity.Netcode;
using UnityEngine;

public class IcePillarEvent : EventBasic
{
    [SerializeField] private GameObject _Icepillar = null;
    public override void Event()
    {
        isEvent = true;
        Debug.Log("氷の柱です");
        //氷柱イベントのマネージャーを生成
        GameObject instance = Instantiate(_Icepillar, new Vector3(0f, 0f, 0f), Quaternion.identity);
        instance.GetComponent<NetworkObject>().Spawn(true);

        //イベントを開始
        instance.GetComponent<IcePillarManager>().StartEvent_Server();
    }

    public override void Destroy()
    {
        
    }
}
