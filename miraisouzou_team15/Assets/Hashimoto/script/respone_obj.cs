using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class respone_obj : MonoBehaviour
{
	[SerializeField] string[] targetTags = { "Obstacles", "Sweets" };

	[Header("リスポーンまでの待機時間（秒）")]
	[SerializeField] private float respawnDelay = 1f;  // インスペクターから調整可能

	void OnTriggerEnter(Collider other)
	{
		if (!PassesTagFilter(other)) return;

		// Rigidbody があれば一旦止めておく
		var rb = other.attachedRigidbody;
		if (rb)
		{
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}

		// 即リセットせず、コルーチンで待機 → Reset 実行
		StartCoroutine(RespawnAfterDelay(other.gameObject));
	}

	private IEnumerator RespawnAfterDelay(GameObject obj)
	{
		yield return new WaitForSeconds(respawnDelay);

		// SendMessage で SetReset() を呼ぶ（存在しなければ無視）
		obj.SendMessage("SetReset", SendMessageOptions.DontRequireReceiver);
		obj.SendMessageUpwards("SetReset", SendMessageOptions.DontRequireReceiver);
	}

	private bool PassesTagFilter(Collider other)
	{
		if (targetTags == null || targetTags.Length == 0) return true;
		foreach (var t in targetTags)
			if (other.CompareTag(t)) return true;
		return false;
	}

	
}
