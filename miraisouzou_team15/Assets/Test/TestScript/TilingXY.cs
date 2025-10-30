using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 指定したPrefabをXY平面にタイル状に並べるスクリプト。
/// 左下のタイルを親にする。
/// </summary>
public class TilingXY : MonoBehaviour
{
	[Header("タイルのPrefab")]
	[SerializeField] private GameObject tilePrefab;

	[Header("横（X）と縦（Y）の数")]
	[SerializeField, Min(1)] private int tileCountX = 5;
	[SerializeField, Min(1)] private int tileCountY = 5;

	[Header("タイル同士の間隔")]
	[SerializeField] private float spacing = 1f;

	/// <summary>
	/// 実際にタイルを生成する処理
	/// </summary>
	public void GenerateTiles()
	{
		if (!tilePrefab)
		{
			Debug.LogError("tilePrefabが設定されていないよ！");
			return;
		}

		// 既存の子を消す（再生成用）
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			DestroyImmediate(transform.GetChild(i).gameObject);
		}

		GameObject parent = null;

		for (int y = 0; y < tileCountY; y++)
		{
			for (int x = 0; x < tileCountX; x++)
			{
				Vector3 pos = new Vector3(
					x * spacing,
					y * spacing,
					0f
				);

				GameObject obj = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

				if (x == 0 && y == 0)
				{
					parent = obj;
				}
				else
				{
					obj.transform.SetParent(parent.transform);
				}
			}
		}

		Debug.Log("タイル生成が完了したよ！");
	}
}

#if UNITY_EDITOR
/// <summary>
/// Inspectorに「Generate Tiles」ボタンを追加
/// </summary>
[CustomEditor(typeof(TilingXY))]
public class TileGeneratorXYEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		TilingXY generator = (TilingXY)target;
		if (GUILayout.Button("Generate Tiles"))
		{
			generator.GenerateTiles();
		}
	}
}
#endif
