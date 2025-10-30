using UnityEngine;

public class BlobShadowPlacer : MonoBehaviour
{
	[Header("設定")]
	public Transform shadowQuad;     // BlobのQuad(子)
	public LayerMask groundMask = ~0;
	public float maxRay = 5f;
	public float yOffset = 0.02f;    // わずかに浮かせてZファイト回避
	public float baseRadius = 0.6f;  // 地面にいる時の半径
	public float airShrink = 0.4f;   // 空中時に縮む比率(0..1)

	[Header("オプション：ライト方向に楕円化")]
	public Light mainLight;
	[Range(0.5f, 2f)] public float ellipseStretch = 1.2f; // 伸ばし係数

	void LateUpdate()
	{
		if (!shadowQuad) return;
		Vector3 origin = transform.position + Vector3.up * 0.1f;
		if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxRay, groundMask))
		{
			// 位置＆法線に合わせる
			shadowQuad.position = hit.point + hit.normal * yOffset;
			shadowQuad.up = hit.normal;

			// 高さで半径変化（浮いたら小さく）
			float h = Mathf.Clamp01(hit.distance / maxRay);
			float radius = Mathf.Lerp(baseRadius, baseRadius * airShrink, h);

			// ライト方向に楕円化（任意）
			Vector3 fwd = Vector3.Cross(shadowQuad.right, hit.normal); // 接地面上のforward
			Vector3 right = Vector3.Cross(hit.normal, (mainLight ? -mainLight.transform.forward : fwd)).normalized;
			Vector3 forward = Vector3.Cross(right, hit.normal).normalized;

			float stretch = mainLight ? ellipseStretch : 1f;
			shadowQuad.rotation = Quaternion.LookRotation(forward, hit.normal);
			shadowQuad.localScale = new Vector3(radius * stretch, 1f, radius);
		}
		else
		{
			// 地面がないときは小さくして足元に留める
			Vector3 p = transform.position + Vector3.down * 0.05f;
			shadowQuad.position = p;
			shadowQuad.up = Vector3.up;
			shadowQuad.localScale = Vector3.one * baseRadius * airShrink;
		}
	}
}
