using Unity.Netcode;
using UnityEngine;

public class KonpeitougunEvent : EventBasic
{
    [SerializeField] private GameObject _konpeitou = null;
    private GameObject _konpeitouManager = null;

    public override void Event()
    {
        isEvent = true;
        Debug.Log("金平糖群です");
        //金平糖群マネージャーを生成
        _konpeitouManager = Instantiate(_konpeitou,new Vector3(0f,0f,0f),Quaternion.identity);
        _konpeitouManager.GetComponent<NetworkObject>().Spawn(true);

        var konpe = _konpeitouManager.GetComponent<KonpeitougunManager>();

        konpe._konpeitougun = this;
        //イベントを開始
        konpe.StartEvent_Server();

        //終わったら消すよ
        Destroy();
    }

    

    public override void Destroy()
    {
        //マネージャーとイベント呼び出しを消す
        Destroy(_konpeitouManager,_eventTime);
        Destroy(gameObject,_eventTime);
    }
}
