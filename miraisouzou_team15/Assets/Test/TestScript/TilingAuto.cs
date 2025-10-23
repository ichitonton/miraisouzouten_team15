using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 指定サイズのグリッドにPrefabを敷き詰める（3D）
/// - 原点はこのコンポーネントの transform.position
/// - Width = 横（X）、Height = 縦（Z）
/// - CellSize でマスの大きさを決める（1mキューブなら (1,1,1)）
/// - Centerize をONにすると全体が原点中心に来る
/// </summary>
public class TilingAuto : MonoBehaviour
{
	[Header("並べるPrefab（必須）")]
	public GameObject prefab;

	[Header("グリッドサイズ")]
	[Min(1)] public int width = 3;   // 横（列数, X方向）
	[Min(1)] public int height = 4;   // 縦（行数, Z方向）

	[Header("セル設定")]
	public Vector3 cellSize = new Vector3(1f, 1f, 1f); // 1マスのサイズ
	public Vector2 spacing = Vector2.zero;            // マス間のすき間（X/Z）

	[Tooltip("原点(=このオブジェクトの位置)をマップの中心に合わせる")]
	public bool centerize = false;

	[Tooltip("生成前に、子オブジェクトを全消去する")]
	public bool clearChildrenBefore = true;

	[Header("高さオフセット（地面にめり込む/浮く調整）")]
	public float yOffset = 0f;

	// 実効セルサイズ（すき間込み）
	Vector3 Step => new Vector3(
		cellSize.x + spacing.x,
		cellSize.y,
		cellSize.z + spacing.y
	);

	/// <summary>実際に並べる</summary>
	public void Generate()
	{
		if (!prefab)
		{
			Debug.LogWarning("[GridInstantiator3D] Prefab が未設定。");
			return;
		}

		if (clearChildrenBefore)
		{
			ClearChildren();
		}

		// 中央寄せのための原点オフセット算出
		Vector3 origin = transform.position;
		if (centerize)
		{
			float totalX = (width - 1) * Step.x;
			float totalZ = (height - 1) * Step.z;
			origin -= new Vector3(totalX * 0.5f, 0f, totalZ * 0.5f);
		}

		// Z方向に行（上から並べたいならループ順を変えてOK）
		for (int z = 0; z < height; z++)
		{
			for (int x = 0; x < width; x++)
			{
				Vector3 pos = new Vector3(
					origin.x + x * Step.x,
					origin.y + yOffset,
					origin.z + z * Step.z
				);

				// 生成
				GameObject go = null;

#if UNITY_EDITOR
				// エディタ上（再生してなくても）Undo対応で生成
				if (!Application.isPlaying)
				{
					go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
					go.transform.SetPositionAndRotation(pos, prefab.transform.rotation);
				}
				else
#endif
				{
					go = Instantiate(prefab, pos, prefab.transform.rotation, transform);
				}

				go.name = $"{prefab.name}_x{x}_z{z}";
				// ローカルスケールはPrefabに従う。マスごとに拡大縮小したいなら以下を解放
				// go.transform.localScale = prefab.transform.localScale;
			}
		}
	}

	/// <summary>子オブジェクト全削除</summary>
	public void ClearChildren()
	{
		// 走行中とエディタで削除方法を分ける
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			var c = transform.GetChild(i);
#if UNITY_EDITOR
			if (!Application.isPlaying)
				DestroyImmediate(c.gameObject);
			else
#endif
				Destroy(c.gameObject);
		}
	}

	// シーン上で目安のグリッドを描く（Gizmos）
	private void OnDrawGizmosSelected()
	{
		Gizmos.matrix = Matrix4x4.identity;

		Vector3 origin = transform.position;
		if (centerize)
		{
			float totalX = (width - 1) * Step.x;
			float totalZ = (height - 1) * Step.z;
			origin -= new Vector3(totalX * 0.5f, 0f, totalZ * 0.5f);
		}

		Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
		for (int z = 0; z < height; z++)
		{
			for (int x = 0; x < width; x++)
			{
				Vector3 center = new Vector3(
					origin.x + x * Step.x,
					origin.y + yOffset,
					origin.z + z * Step.z
				);
				Vector3 size = new Vector3(cellSize.x, 0.01f, cellSize.z);
				Gizmos.DrawWireCube(center + Vector3.up * 0.01f, size);
			}
		}
	}

#if UNITY_EDITOR
	// インスペクタにボタン追加（Editor拡張を同一ファイルで簡易実装）
	[CustomEditor(typeof(TilingAuto))]
	private class GridInstantiator3DEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var t = (TilingAuto)target;

			GUILayout.Space(8);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Generate Grid", GUILayout.Height(28)))
				{
					t.Generate();
				}
				if (GUILayout.Button("Clear Children", GUILayout.Height(28)))
				{
					t.ClearChildren();
				}
			}

			EditorGUILayout.HelpBox(
				"・Width=X列, Height=Z行\n" +
				"・Centerizeで全体を原点中心に配置\n" +
				"・Spacingでマス間の隙間（X/Z）\n" +
				"・Gizmosは選択時にプレビュー表示",
				MessageType.Info
			);
		}
	}
#endif
}
