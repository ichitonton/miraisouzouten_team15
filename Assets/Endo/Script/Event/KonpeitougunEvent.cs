using Unity.Netcode;
using UnityEngine;

public class KonpeitougunEvent : EventBasic
{
    [SerializeField] private GameObject _konpeitou = null;


    public override void Event()
    {
        isEvent = true;
        Debug.Log("金平糖群です");
        //金平糖群マネージャーを生成
        GameObject instance = Instantiate(_konpeitou,new Vector3(0f,0f,0f),Quaternion.identity);
        instance.GetComponent<NetworkObject>().Spawn(true);

        //イベントを開始
        instance.GetComponent<KonpeitougunManager>().StartEvent_Server();
    }
}
