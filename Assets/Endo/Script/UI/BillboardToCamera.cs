using UnityEngine;

/// <summary>
/// ワールド空間UIを常にカメラへ向ける
/// </summary>
public class BillboardToCamera : MonoBehaviour
{
    [SerializeField] private bool lockZ = true; // 傾きを防ぐ（基本ON）

    private Camera _cam;

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        Vector3 dir = transform.position - _cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);

        if (lockZ)
        {
            Vector3 e = rot.eulerAngles;
            e.z = 0f;
            rot = Quaternion.Euler(e);
        }

        transform.rotation = rot;
    }
}
