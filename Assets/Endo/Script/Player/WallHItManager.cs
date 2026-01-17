using UnityEngine;

public class WallHItManager : MonoBehaviour
{
    [Header("Effect")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Hit Filter")]
    [SerializeField] private LayerMask wallMask;        // 壁レイヤーを指定
    [SerializeField] private float minImpactSpeed = 2f; // これ以下なら出さない（弱ヒット無視）
    [SerializeField] private float cooldown = 0.15f;    // 連続発生防止

    [Header("Spawn Offset")]
    [SerializeField] private float normalOffset = 0.03f; // 壁に埋まらないように少し浮かせる

    private float _nextAllowedTime = 0f;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < _nextAllowedTime) return;

        // 壁レイヤー以外は無視
        if (((1 << collision.gameObject.layer) & wallMask) == 0) return;

        // 速度が小さいなら出さない（カス当たり防止）
        float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
        if (speed < minImpactSpeed) return;

        if (collision.contactCount == 0) return;

        // ぶつかった点 & 法線（壁からプレイヤーへ向く）
        ContactPoint c = collision.GetContact(0);
        Vector3 hitPoint = c.point;
        Vector3 normal = c.normal;

        // エフェクト位置（壁の外側へ少し押し出す）
        Vector3 spawnPos = hitPoint + normal * normalOffset;

        // エフェクトの向き：
        // 例）壁に貼り付くように出したい → normalの逆を前方向にする
        Quaternion rot = Quaternion.LookRotation(-normal, Vector3.up);

        

        _nextAllowedTime = Time.time + cooldown;
    }
}
