using UnityEngine;

/// <summary>
/// プリンみたいにプルプル揺れる（回転ぷるぷる）
/// - 衝突時の相対速度からインパルスを作る
/// - 接触法線を使って「押された方向へ倒れる」感じを出す
/// - 2次系（ばね＋減衰）で自然に収束
/// </summary>
public class WagashiPrupruDeformer : MonoBehaviour
{
    [System.Serializable]
    public struct PrupruSettings
    {
        public float minImpactVelocity;
        public float maxImpactVelocity;

        public float wobbleFrequency;
        public float wobbleDamping;
        public float wobbleImpulse;
        public float maxTiltAngle;

        public float normalInfluence;
        public float verticalResponse;

        public float extraDecay;
    }

    // 内部パラメータ（SerializeFieldのままでもOKだが、ここはコードで持つ）
    private PrupruSettings _s;

    private Quaternion _baseLocalRot;
    private Vector3 _rotOffsetDeg;
    private Vector3 _rotVelDeg;

    public void ApplySettings(PrupruSettings settings)
    {
        _s = settings;
        _baseLocalRot = transform.localRotation;

        // 状態リセット（切り替え時に暴れないように）
        _rotOffsetDeg = Vector3.zero;
        _rotVelDeg = Vector3.zero;
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact < _s.minImpactVelocity) return;

        float t = Mathf.InverseLerp(_s.minImpactVelocity, _s.maxImpactVelocity, impact);
        t = Mathf.Clamp01(t);

        Vector3 n = AverageContactNormal(collision);
        if (n.sqrMagnitude < 1e-6f) return;

        Vector3 pushDirLocal = transform.InverseTransformDirection(-n).normalized;
        pushDirLocal.y *= _s.verticalResponse;
        if (pushDirLocal.sqrMagnitude < 1e-6f) return;
        pushDirLocal.Normalize();

        Vector3 tiltAxisLocal = new Vector3(pushDirLocal.z, 0f, -pushDirLocal.x);
        if (tiltAxisLocal.sqrMagnitude < 1e-6f) tiltAxisLocal = Vector3.right;
        tiltAxisLocal.Normalize();

        tiltAxisLocal *= Mathf.Max(0f, _s.normalInfluence);

        _rotVelDeg += tiltAxisLocal * (_s.wobbleImpulse * t);
    }

    private Vector3 AverageContactNormal(Collision collision)
    {
        if (collision.contactCount <= 0) return Vector3.up;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++)
            sum += collision.GetContact(i).normal;
        return (sum / collision.contactCount).normalized;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        StepSecondOrder(ref _rotOffsetDeg, ref _rotVelDeg, _s.wobbleFrequency, _s.wobbleDamping, dt);

        _rotOffsetDeg = Vector3.ClampMagnitude(_rotOffsetDeg, _s.maxTiltAngle);

        float decay = Mathf.Clamp01(_s.extraDecay);
        _rotOffsetDeg *= decay;
        _rotVelDeg *= decay;

        transform.localRotation = _baseLocalRot * Quaternion.Euler(_rotOffsetDeg);
    }

    private static void StepSecondOrder(ref Vector3 x, ref Vector3 v, float freqHz, float damping, float dt)
    {
        float omega = 2f * Mathf.PI * Mathf.Max(0.01f, freqHz);
        float zeta = Mathf.Clamp01(damping);

        Vector3 a = (-2f * zeta * omega) * v + (-omega * omega) * x;
        v += a * dt;
        x += v * dt;
    }
}
