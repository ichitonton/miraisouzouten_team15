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

	[Header("対象フィルタ")]
	[Tooltip("このレイヤーに属する相手だけを子化する（例：Player, Item 等）。何も指定しない場合は全て許可。")]
	[SerializeField] LayerMask targetLayers = ~0;

	[Tooltip("Rigidbody の Sleep/補間などを触らない場合は false のままでOK")]
	[SerializeField] bool tryEnableInterpolationForRB = true;

	// すでに子化した相手と、その相手の元の親を覚えておく
	readonly Dictionary<Transform, Transform> originalParents = new Dictionary<Transform, Transform>();

	// 退出検出漏れ対策（TriggerでたまにExit取りこぼすケースなど）
	readonly HashSet<Transform> insideNow = new HashSet<Transform>();

	Collider col;

	Transform memo;

	void Awake()
	{
		col = GetComponent<Collider>();
		col.isTrigger = useTrigger;
	}

    //private void Update()
    //{
		
    //}

    // -------- 衝突検出（useTrigger=false のとき） --------
    void OnCollisionEnter(Collision c)
	{
		if (useTrigger) return;
		TryAttach(c.transform);
	}

	void OnCollisionStay(Collision c)
	{
		if (useTrigger) return;
		// Stayで継続管理（万一Enter取りこぼしても拾う）
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

		// すでにこの床の子になってるなら何もしない
		if (target.parent == transform) return;

		// すでに記録済み（他の床から移ってきた等）なら、いったん元に戻してから付け替え（安全策）
		if (originalParents.TryGetValue(target, out var recordedParent) && target.parent != recordedParent)
		{
			target.SetParent(recordedParent, true);
		}

		// 元の親を記録（未記録のときだけ）
		if (!originalParents.ContainsKey(target))
			originalParents[target] = target.parent;

		// 子化（ワールド位置維持）
		target.SetParent(transform, true);

		// 物理的にガタつく場合の微サポート
		if (tryEnableInterpolationForRB && target.TryGetComponent<Rigidbody>(out var rb))
		{
			if (rb.interpolation == RigidbodyInterpolation.None)
				rb.interpolation = RigidbodyInterpolation.Interpolate;
		}

		//スケール固定
        //transform.c= target.lossyScale;
		
    }

	// 解除処理
	void TryDetach(Transform target)
	{
		if (!originalParents.ContainsKey(target)) return;

		// まだ床の子じゃなければスキップ（別の親に付け替わった等）
		if (target.parent != transform && target.parent != null)
		{
			// 他が親になっている場合は記録を消すだけ
			originalParents.Remove(target);
			insideNow.Remove(target);
			return;
		}

		// 元の親に戻す（ワールド位置維持）
		var original = originalParents[target];
		target.SetParent(original, true);

		originalParents.Remove(target);
		insideNow.Remove(target);
	}

	// 何かの拍子でExit取りこぼした場合の救済（任意）
	void LateUpdate()
	{
		if (!useTrigger) return; // Trigger運用の時だけ保険をかける
								 // 床のバウンディングに実際にいないやつを外す
								 // （AABBで雑にチェック：厳密でなくてOK）
		var bounds = col.bounds;
		// コピーしてから回す（集合を途中でいじらない）
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

	// 対象フィルタ（レイヤー判定は子のrootでもOKにしたい場合は調整してね）
	bool IsTarget(Transform t)
	{
		if (!t) return false;
		int layer = t.gameObject.layer;
		return (targetLayers.value & (1 << layer)) != 0;
	}

	// 無効化・破棄時は全部元に戻す
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
