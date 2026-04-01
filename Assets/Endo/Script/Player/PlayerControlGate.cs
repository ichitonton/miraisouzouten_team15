using UnityEngine;

public class PlayerControlGate : MonoBehaviour
{
    [Header("Lock State")]
    [SerializeField] private bool locked = false;
    public bool IsLocked => locked;

    [Header("Disable these behaviours while locked")]
    [Tooltip("MovePlayerKeyLocal など、入力を読むスクリプトを入れる")]
    [SerializeField] private Behaviour[] disableOnLock;

    // 既存仕様に合わせて public
    public void SetLocked(bool isLocked)
    {
        locked = isLocked;

        if (disableOnLock != null)
        {
            for (int i = 0; i < disableOnLock.Length; i++)
            {
                var b = disableOnLock[i];
                if (b == null) continue;
                b.enabled = !locked;
            }
        }
    }

    // （任意）起動時反映
    private void OnEnable()
    {
        SetLocked(locked);
    }
}
