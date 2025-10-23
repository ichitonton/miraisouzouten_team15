using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SwampArea : MonoBehaviour
{
	[Header("初期化設定")]
	[Tooltip("起動時にこのタグを設定します（Player側と合わせる）")]
	public string swampTag = "Swamp";

	void Reset()
	{
		var col = GetComponent<BoxCollider>();
		col.isTrigger = true;              // 置いた瞬間からTriggerに
		gameObject.tag = swampTag;         // タグも自動設定
	}

	void OnValidate()
	{
		var col = GetComponent<BoxCollider>();
		if (col != null && !col.isTrigger) col.isTrigger = true; // Inspector変更時も保つ
		if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
			gameObject.tag = swampTag;     // タグ未設定なら補完
	}

#if UNITY_EDITOR
	void OnDrawGizmos()
	{
		var col = GetComponent<BoxCollider>();
		if (!col) return;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawWireCube(col.center, col.size);
	}
#endif
}
