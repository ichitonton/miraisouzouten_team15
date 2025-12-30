using UnityEngine;
using Unity.Netcode;

public abstract class EventBasic : NetworkBehaviour
{
    [Header("Event Basic")]
    [SerializeField] protected bool isEvent = true;

    [SerializeField] protected string _eventName = "EventId";

    /// <summary>
    /// 外部参照用（読み取りだけにするのがおすすめ）
    /// </summary>
    public bool IsEvent => isEvent;
    public string EventName => _eventName;

    /// <summary>
    /// イベント本体（派生クラスで必ず実装する）
    /// </summary>
    public abstract void Event();

    /// <summary>
    /// Manager 側から安全に呼び出すための入口（isEventチェック込み）
    /// </summary>
    public void TryEvent()
    {
        //裴瀬イベントの呼び出し
        Event();
    }

}
