using System.Collections.Generic;
using UnityEngine;

// 動く床に乗ってる間だけ、乗った相手を一時的に子オブジェにする。
// 床にアタッチして使う。

[RequireComponent(typeof(Collider))]
public class movingPlatformPassenger : MonoBehaviour
{
	[Header("検出方法")]
	[Tooltip("true: この床のColliderがTriggerで、OnTriggerで検出する / false: 通常の衝突で検出する")]
	[SerializeField] bool useTrigger = false;

	[Header("対象フィルタ（タグ版）")]
	[Tooltip("ここに列挙したタグの相手だけ子化する。空配列なら全て許可。")]
	[SerializeField] string[] targetTags = new string[0];

	[Tooltip("相手の root(最上位) のタグも見るなら true。子のColliderにタグが無い構成向け。")]
	[SerializeField] bool alsoCheckRootTag = true;

	[Tooltip("Rigidbody の Sleep/補間などを触らない場合は false のままでOK")]
	[SerializeField] bool tryEnableInterpolationForRB = true;

	readonly Dictionary<Transform, Transform> originalParents = new Dictionary<Transform, Transform>();
	readonly HashSet<Transform> insideNow = new HashSet<Transform>();

	Collider col;

	void Awake()
	{
		col = GetComponent<Collider>();
		col.isTrigger = useTrigger;
	}

	// -------- 衝突検出（useTrigger=false のとき） --------
	void OnCollisionEnter(Collision c)
	{
		if (useTrigger) return;
		TryAttach(c.transform);
	}

	void OnCollisionStay(Collision c)
	{
		if (useTrigger) return;
		TryAttach(c.transform);
	}

	void OnCollisionExit(Collision c)
	{
		if (useTrigger) return;
		TryDetach(c.transform);
	}

	// -------- トリガー検出（useTrigger=true のとき） --------
	void OnTriggerEnter(Collider other)
	{
		if (!useTrigger) return;
		insideNow.Add(other.transform);
		TryAttach(other.transform);
	}

	void OnTriggerStay(Collider other)
	{
		if (!useTrigger) return;
		insideNow.Add(other.transform);
		TryAttach(other.transform);
	}

	void OnTriggerExit(Collider other)
	{
		if (!useTrigger) return;
		insideNow.Remove(other.transform);
		TryDetach(other.transform);
	}

	// 子化処理
	void TryAttach(Transform target)
	{
		if (!IsTarget(target)) return;
		if (target.parent == transform) return;

		if (originalParents.TryGetValue(target, out var recordedParent) && target.parent != recordedParent)
		{
			target.SetParent(recordedParent, true);
		}

		if (!originalParents.ContainsKey(target))
			originalParents[target] = target.parent;

		target.SetParent(transform, true);

		if (tryEnableInterpolationForRB && target.TryGetComponent<Rigidbody>(out var rb))
		{
			if (rb.interpolation == RigidbodyInterpolation.None)
				rb.interpolation = RigidbodyInterpolation.Interpolate;
		}
	}

	// 解除処理
	void TryDetach(Transform target)
	{
		if (!originalParents.ContainsKey(target)) return;

		if (target.parent != transform && target.parent != null)
		{
			originalParents.Remove(target);
			insideNow.Remove(target);
			return;
		}

		var original = originalParents[target];
		target.SetParent(original, true);

		originalParents.Remove(target);
		insideNow.Remove(target);
	}

	// Exit取りこぼし救済（Trigger運用時のみ）
	void LateUpdate()
	{
		if (!useTrigger) return;

		var bounds = col.bounds;
		var snapshot = new List<Transform>(originalParents.Keys);
		foreach (var t in snapshot)
		{
			if (!t) { originalParents.Remove(t); continue; }
			if (!bounds.Contains(t.position))
			{
				TryDetach(t);
			}
		}
		insideNow.Clear();
	}

	// 対象フィルタ（タグ判定）
	bool IsTarget(Transform t)
	{
		if (!t) return false;
		if (targetTags == null || targetTags.Length == 0) return true;

		foreach (var tag in targetTags)
		{
			if (string.IsNullOrEmpty(tag)) continue;
			// 子（このColliderが付いてるオブジェクト）を判定
			if (t.CompareTag(tag)) return true;

			// ルート（プレイヤー本体など）も見るオプション
			if (alsoCheckRootTag && t.root && t.root.CompareTag(tag)) return true;
		}
		return false;
	}

	void OnDisable() => DetachAll();
	void OnDestroy() => DetachAll();

	void DetachAll()
	{
		var snapshot = new List<Transform>(originalParents.Keys);
		foreach (var t in snapshot)
		{
			if (!t) continue;
			TryDetach(t);
		}
		originalParents.Clear();
		insideNow.Clear();
	}
}
