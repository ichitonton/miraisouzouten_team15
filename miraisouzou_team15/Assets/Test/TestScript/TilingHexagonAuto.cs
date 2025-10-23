using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


// 正六角形の“領域”を六角形タイルで敷き詰めて生成する（3D）
//  - 形は中心から半径Rのヘクス（HexRadius）
//  - Orientation: Flat-Top（平頭）/ Pointy-Top（角頭）
//  - CellRadius: 六角形1タイルの内接円半径ではなく“外接円半径”想定（中心→頂点距離）
//    * Flat-Top の幅 = 2*CellRadius、奥行 ≒ √3*CellRadius
//    * Pointy-Top の幅 ≒ √3*CellRadius、高さ = 2*CellRadius
//  - 原点はこのコンポーネントの transform.position（Centerizeで中央寄せ）

public class TilingHexagonAuto : MonoBehaviour
{
	public enum HexOrientation { FlatTop, PointyTop }

	[Header("並べるタイル（Prefab）")]
	public GameObject tilePrefab;

	[Header("六角形領域サイズ")]
	[Min(0)] public int hexRadius = 3; // 中心からのリング数（0=中心だけ）

	[Header("六角の向き")]
	public HexOrientation orientation = HexOrientation.FlatTop;

	[Header("セルのスケール設定")]
	[Tooltip("六角の外接円半径（中心→頂点の距離, メートル想定）")]
	public float cellRadius = 0.5f;

	[Tooltip("隣接タイルの間隔（世界座標のX/Z方向に加算）")]
	public Vector2 gap = Vector2.zero; // x: 横方向, y: 縦方向の隙間

	[Header("配置オプション")]
	public bool centerize = true;
	public float yOffset = 0f;
	public bool clearChildrenBefore = true;

	// ---- Axial座標系（q, r）で計算 ----
	// Flat-Top:
	//   worldX = size * (3/2 * q)
	//   worldZ = size * (√3 * (r + q/2))
	// Pointy-Top:
	//   worldX = size * (√3 * (q + r/2))
	//   worldZ = size * (3/2 * r)

	public void Generate()
	{
		if (!tilePrefab)
		{
			Debug.LogWarning("[HexGridInstantiator3D] tilePrefab が未設定。");
			return;
		}

		if (clearChildrenBefore) ClearChildren();

		// まず全タイルのワールド位置をリスト化（センタリング計算のため）
		var positions = new System.Collections.Generic.List<Vector3>();

		for (int q = -hexRadius; q <= hexRadius; q++)
		{
			int rMin = Mathf.Max(-hexRadius, -q - hexRadius);
			int rMax = Mathf.Min(hexRadius, -q + hexRadius);
			for (int r = rMin; r <= rMax; r++)
			{
				// s = -q - r は省略（制約 |q|,|r|,|s| <= hexRadius）
				var world = AxialToWorld(q, r);
				positions.Add(world);
			}
		}

		// 中央寄せオフセット
		Vector3 centerOffset = Vector3.zero;
		if (centerize && positions.Count > 0)
		{
			Vector3 sum = Vector3.zero;
			foreach (var p in positions) sum += p;
			Vector3 avg = sum / positions.Count;
			centerOffset = -avg;
		}

		// 実生成
		int index = 0;
		foreach (var p in positions)
		{
			Vector3 pos = transform.position + p + centerOffset + new Vector3(0f, yOffset, 0f);

			GameObject go = null;
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				go = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, transform);
				go.transform.SetPositionAndRotation(pos, tilePrefab.transform.rotation);
			}
			else
#endif
			{
				go = Instantiate(tilePrefab, pos, tilePrefab.transform.rotation, transform);
			}

			go.name = $"{tilePrefab.name}_{index++}";
		}
	}

	// Axial(q,r) → World(X,Z)
	private Vector3 AxialToWorld(int q, int r)
	{
		float x, z;
		float s = cellRadius;

		if (orientation == HexOrientation.FlatTop)
		{
			// Flat-Top
			x = s * (1.5f * q);                                   // 3/2 * q
			z = s * (Mathf.Sqrt(3f) * (r + 0.5f * q));            // √3 * (r + q/2)
			x += gap.x * q;
			z += gap.y * (r + 0.5f * q);
		}
		else
		{
			// Pointy-Top
			x = s * (Mathf.Sqrt(3f) * (q + 0.5f * r));            // √3 * (q + r/2)
			z = s * (1.5f * r);                                   // 3/2 * r
			x += gap.x * (q + 0.5f * r);
			z += gap.y * r;
		}

		return new Vector3(x, 0f, z);
	}

	public void ClearChildren()
	{
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			var c = transform.GetChild(i);
#if UNITY_EDITOR
			if (!Application.isPlaying) DestroyImmediate(c.gameObject);
			else
#endif
				Destroy(c.gameObject);
		}
	}

	private void OnDrawGizmosSelected()
	{
		// プレビュー用ワイヤー表示（中心基準）
		Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.6f);

		var positions = new System.Collections.Generic.List<Vector3>();
		for (int q = -hexRadius; q <= hexRadius; q++)
		{
			int rMin = Mathf.Max(-hexRadius, -q - hexRadius);
			int rMax = Mathf.Min(hexRadius, -q + hexRadius);
			for (int r = rMin; r <= rMax; r++)
			{
				positions.Add(AxialToWorld(q, r));
			}
		}

		Vector3 centerOffset = Vector3.zero;
		if (centerize && positions.Count > 0)
		{
			Vector3 sum = Vector3.zero;
			foreach (var p in positions) sum += p;
			Vector3 avg = sum / positions.Count;
			centerOffset = -avg;
		}

		foreach (var p in positions)
		{
			Vector3 c = transform.position + p + centerOffset + Vector3.up * (0.01f + yOffset);
			DrawHexWire(c, cellRadius, orientation);
		}
	}

	// 簡易ワイヤー六角形
	private void DrawHexWire(Vector3 center, float radius, HexOrientation o)
	{
		Vector3[] pts = new Vector3[6];
		for (int i = 0; i < 6; i++)
		{
			float angleDeg = (o == HexOrientation.FlatTop)
				? (60f * i + 0f)       // Flat-Top：0,60,120...
				: (60f * i + 30f);     // Pointy-Top：30,90,150...
			float rad = angleDeg * Mathf.Deg2Rad;
			pts[i] = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
		}
		for (int i = 0; i < 6; i++)
		{
			Gizmos.DrawLine(pts[i], pts[(i + 1) % 6]);
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(TilingHexagonAuto))]
	private class HexGridInstantiator3DEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var t = (TilingHexagonAuto)target;

			GUILayout.Space(8);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Generate Hex Grid", GUILayout.Height(28)))
				{
					t.Generate();
				}
				if (GUILayout.Button("Clear Children", GUILayout.Height(28)))
				{
					t.ClearChildren();
				}
			}

			EditorGUILayout.HelpBox(
				"・HexRadius: 中心からのリング数（0=1枚）\n" +
				"・Orientation: 平頭(Flat) / 角頭(Pointy)\n" +
				"・CellRadius: 六角の外接円半径（中心→頂点）\n" +
				"・Gap: タイル間の隙間（X/Z方向）\n" +
				"・Centerize: 原点に全体を中央寄せ",
				MessageType.Info
			);
		}
	}
#endif
}
