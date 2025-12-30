using UnityEngine;

public class KonpeitougunEvent : EventBasic
{
    [SerializeField] private GameObject _konpeitou = null;
    public override void Event()
    {
        isEvent = true;
        Debug.Log("‹à•½“œŒQ‚Å‚·");
    }
}
