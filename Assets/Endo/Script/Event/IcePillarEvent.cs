using Unity.Netcode;
using UnityEngine;

public class IcePillarEvent : EventBasic
{
    [SerializeField] private GameObject _Icepillar = null;
    private GameObject _iceManager = null;
    public override void Event()
    {
        isEvent = true;
        Debug.Log("氷の柱です");
        //氷柱イベントのマネージャーを生成
        _iceManager = Instantiate(_Icepillar, new Vector3(0f, 0f, 0f), Quaternion.identity);
        _iceManager.GetComponent<NetworkObject>().Spawn(true);

        //イベントを開始
        _iceManager.GetComponent<IcePillarManager>().StartEvent_Server();

        _eventTime = 20f;

        Destroy();
    }

    public override void Destroy()
    {

        Destroy(_iceManager, _eventTime);
        Destroy(gameObject, _eventTime);

    }
}
