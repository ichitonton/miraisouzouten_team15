using UnityEngine;

public class CanJump : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;  // 地面レイヤー
    private int groundCount = 0;

    public bool CanJumpNow => groundCount > 0; // 自動判定

    void OnTriggerEnter(Collider collider)
    {
        if (((1 << collider.gameObject.layer) & groundLayer.value) != 0)
        {
            groundCount++;
        }
    }

    void OnTriggerExit(Collider collider)
    {
        if (((1 << collider.gameObject.layer) & groundLayer.value) != 0)
        {
            groundCount--;
            if (groundCount < 0) groundCount = 0; // 安全策
        }
    }
}
