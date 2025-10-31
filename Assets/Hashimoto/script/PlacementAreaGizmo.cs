using UnityEngine;

public class PlacementAreaGizmo : MonoBehaviour
{
	[Header("エリアサイズ")]
	public Vector3 areaSize = new Vector3(5, 1, 5);

	[Header("色設定")]
	public Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // 半透明の緑

	void OnDrawGizmos()
	{
		// ワイヤーで枠を表示
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(transform.position, areaSize);

		// 半透明で塗りつぶし（範囲がわかりやすい）
		Gizmos.color = gizmoColor;
		Gizmos.DrawCube(transform.position, areaSize);
	}
}
