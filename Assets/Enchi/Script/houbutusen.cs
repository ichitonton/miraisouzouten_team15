using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ParabolicThrow : MonoBehaviour
{
    [Header("投げる設定")]
    public Transform target;          // 狙うターゲット
    public GameObject projectilePrefab; // 投げるオブジェクト
    public float flightTime = 1.0f;   // 目標到達までの時間（秒）

    [Header("描画設定")]
    public int trajectorySteps = 30;  // 放物線の分割数
    public bool showTrajectory = true; // 軌道を表示するか

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // スペースキーで投げる
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Throw();
        }

        // 軌道の可視化
        if (showTrajectory && target)
        {
            Vector3 velocity = CalculateVelocity(target.position, transform.position, flightTime);
            DrawTrajectory(transform.position, velocity);
        }
    }

    void Throw()
    {
        if (!target || !projectilePrefab) return;

        // 投げる物体生成
        GameObject obj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        // 初速度を計算して付与
        Vector3 velocity = CalculateVelocity(target.position, transform.position, flightTime);
        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// target に time 秒で到達するための初速度を計算
    /// </summary>
    Vector3 CalculateVelocity(Vector3 target, Vector3 origin, float time)
    {
        Vector3 distance = target - origin;
        Vector3 distanceXZ = new Vector3(distance.x, 0, distance.z);

        float sy = distance.y;
        float sxz = distanceXZ.magnitude;

        Vector3 result = distanceXZ / time; // XZ方向の速度
        result.y = sy / time - 0.5f * Physics.gravity.y * time;

        return result;
    }

    /// <summary>
    /// 放物線軌道を LineRenderer で描画
    /// </summary>
    void DrawTrajectory(Vector3 origin, Vector3 velocity)
    {
        if (!lineRenderer) return;
        lineRenderer.positionCount = trajectorySteps;

        for (int i = 0; i < trajectorySteps; i++)
        {
            float t = (i / (float)(trajectorySteps - 1)) * flightTime;
            Vector3 pos = origin + velocity * t + 0.5f * Physics.gravity * t * t;
            lineRenderer.SetPosition(i, pos);
        }
    }
}
