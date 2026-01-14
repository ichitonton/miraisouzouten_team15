using UnityEngine;

public class HitTrigger : MonoBehaviour
{
    private Jibaku _parent;

    private void Awake()
    {
        _parent = GetComponentInParent<Jibaku>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Punch以外は無視
        if (other.GetComponent<Punch>() != null)
        Debug.Log("エネミーをぱんち！！");
        _parent.Punch();
    }

    public void HitPunch()
    {
        _parent.Punch();
    }
}
