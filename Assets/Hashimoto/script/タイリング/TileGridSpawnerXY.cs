using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// XY 平面に「四角タイル」を手動生成するスパウナー。
/// - 生成はボタンを押したときだけ（自動生成しない）
/// - 生成時は container 直下の子を全削除してから並べ直す
/// - 生成前プレビューは Gizmos のみ表示
/// - Gizmos は Prefab の Renderer 実寸が取れればそれを使用（取れない時は tileSize）
/// - 生成ピッチは tileSize（= X/Y の間隔）。Prefab実寸はプレビュー専用。
/// </summary>
public class TileGridSpawnerXY : MonoBehaviour
{
	[Header("サイズ")]
	[Min(1)] public int xCount = 8;
	[Min(1)] public int yCount = 8;

	[Tooltip("タイルのピッチ（Prefab 実寸が使えない場合のフォールバック値、単位:メートル）")]
	[Min(0.0001f)] public float tileSize = 1.0f;

	[Tooltip("グリッド全体を親の原点（container）にセンタリングする")]
	public bool centerToOrigin = true;

	[Header("見た目（任意）")]
	[Tooltip("四角タイルの Prefab。未指定なら Quad を自動生成（XY向きのまま使用）")]
	public GameObject tilePrefab;

	[Tooltip("生成時に適用するマテリアル（Renderer がある場合のみ）")]
	public Material tileMaterial;

	[Header("生成先")]
	[Tooltip("生成したタイルの親。未指定ならこのコンポーネントの Transform")]
	public Transform container;

	void Reset()
	{
		if (container == null) container = transform;
	}

	// ====== 外部から呼ぶ公開API（エディタ拡張・コンテキストメニューから利用） ======
	public void RebuildGrid()
	{
		ClearChildren();
		BuildGrid();
	}

	public void ClearChildren()
	{
		var parent = container ? container : transform;

#if UNITY_EDITOR
		Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Clear Tiles (XY)");
#endif

		for (int i = parent.childCount - 1; i >= 0; i--)
		{
			var child = parent.GetChild(i);
#if UNITY_EDITOR
			if (!Application.isPlaying)
				Undo.DestroyObjectImmediate(child.gameObject);
			else
				Destroy(child.gameObject);
#else
			Destroy(child.gameObject);
#endif
		}
	}

	public void BuildGrid()
	{
		if (container == null) container = transform;
		if (xCount < 1 || yCount < 1) return;

		// 実際の生成ピッチは tileSize を使用（Prefab 実寸はプレビュー専用）
		float unitX = tileSize;
		float unitY = tileSize;

		Vector3 originOffset = Vector3.zero;
		if (centerToOrigin)
		{
			float totalX = (xCount - 1) * unitX;
			float totalY = (yCount - 1) * unitY;
			originOffset = new Vector3(-totalX * 0.5f, -totalY * 0.5f, 0f);
		}

		for (int y = 0; y < yCount; y++)
		{
			for (int x = 0; x < xCount; x++)
			{
				var localPos = new Vector3(x * unitX, y * unitY, 0f) + originOffset;
				var go = CreateOne(localPos);
				go.name = $"Tile_{x}_{y}";

				if (tileMaterial != null)
				{
					var r = go.GetComponentInChildren<Renderer>();
					if (r != null) r.sharedMaterial = tileMaterial;
				}
			}
		}
	}

	GameObject CreateOne(Vector3 localPos)
	{
		GameObject go = null;

		if (tilePrefab != null)
		{
#if UNITY_EDITOR
			// Prefab アセットなら PrefabUtility 経由、そうでなければ Instantiate
			var assetType = PrefabUtility.GetPrefabAssetType(tilePrefab);
			bool isPrefabAsset = assetType != PrefabAssetType.NotAPrefab;

			if (!Application.isPlaying && isPrefabAsset)
				go = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab);
			else
				go = Instantiate(tilePrefab);
#else
			go = Instantiate(tilePrefab);
#endif
		}
		else
		{
			// Quad はそのまま XY（+Z向き）
			go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = new Vector3(tileSize, tileSize, 1f);
		}

		if (go == null)
		{
			Debug.LogWarning("tilePrefab の生成に失敗。Quad にフォールバックします。(XY)", this);
			go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.transform.localRotation = Quaternion.identity;
			go.transform.localScale = new Vector3(tileSize, tileSize, 1f);
		}

		var parent = container ? container : transform;
		go.transform.SetParent(parent, false);   // 親のローカル空間
		go.transform.localPosition = localPos;   // ローカル座標で配置（Z=0 平面）
		return go;
	}

#if UNITY_EDITOR
	// ====== 生成前プレビュー（Gizmos） ======

	// Prefab の Renderer から実寸（X/Y）を試しに取得。失敗したら tileSize を返す。
	void GetPreviewUnitSize(out float unitX, out float unitY)
	{
		unitX = tileSize;
		unitY = tileSize;

		if (tilePrefab == null) return;

		var rend = tilePrefab.GetComponentInChildren<Renderer>();
		if (rend == null) return;

		// 注意：bounds はワールド空間だが、Prefab アセットでも概ね実寸相当が得られる。
		// 取得できない/極端に小さい場合は tileSize にフォールバック。
		Vector3 size = rend.bounds.size;
		if (size.x > 1e-5f && size.y > 1e-5f)
		{
			unitX = size.x;
			unitY = size.y;
		}
	}

	void OnDrawGizmos()
	{
		DrawPreviewGizmos(false);
	}

	void OnDrawGizmosSelected()
	{
		DrawPreviewGizmos(true);
	}

	void DrawPreviewGizmos(bool selected)
	{
		if (xCount < 1 || yCount < 1) return;

		// container 基準で描画
		var basis = container ? container : transform;
		Gizmos.matrix = basis.localToWorldMatrix;

		// ★ Prefab 実寸が取れればそれを使用
		GetPreviewUnitSize(out float unitX, out float unitY);

		float totalX = (xCount - 1) * unitX;
		float totalY = (yCount - 1) * unitY;
		var offset = centerToOrigin ? new Vector3(-totalX * 0.5f, -totalY * 0.5f, 0f) : Vector3.zero;

		// 外枠
		Gizmos.color = selected ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.6f);
		var p0 = offset + new Vector3(0, 0, 0);
		var p1 = offset + new Vector3(totalX, 0, 0);
		var p2 = offset + new Vector3(totalX, totalY, 0);
		var p3 = offset + new Vector3(0, totalY, 0);
		Gizmos.DrawLine(p0, p1); Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p0);

		// 格子線
		Gizmos.color = new Color(0, 1, 0, selected ? 0.75f : 0.35f);
		for (int x = 0; x < xCount; x++)
		{
			float gx = x * unitX + offset.x;
			Gizmos.DrawLine(new Vector3(gx, offset.y, 0), new Vector3(gx, offset.y + totalY, 0));
		}
		for (int y = 0; y < yCount; y++)
		{
			float gy = y * unitY + offset.y;
			Gizmos.DrawLine(new Vector3(offset.x, gy, 0), new Vector3(offset.x + totalX, gy, 0));
		}
	}

	// ====== カスタムインスペクタが使えないときの保険（ギアから呼べる） ======
	[ContextMenu("グリッドを生成（子を全削除してから）")]
	private void Menu_Rebuild() => RebuildGrid();

	[ContextMenu("子を全削除")]
	private void Menu_Clear() => ClearChildren();
#endif
}
