using UnityEngine;

[RequireComponent(typeof(Collider))]
public class respawn_player : MonoBehaviour
{
	[SerializeField] string playerTag = "Player";

	Vector3 startPos;
	Quaternion startRot;
	Rigidbody rb;

	void Start()
	{
		startPos = transform.position;
		startRot = transform.rotation;
		rb = GetComponent<Rigidbody>();
	}

	void SetReset()
	{
		// 位置・回転を初期状態に戻す
		transform.SetPositionAndRotation(startPos, startRot);

		// 速度を止める（重要）
		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}
	}
	void Reset()
	{
		var col = GetComponent<Collider>();
		col.isTrigger = true;
		// Rigidbodyはプレイヤー側にあるので、こっちには不要！
	}

	void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag(playerTag)) return;

		var pid = other.GetComponentInParent<player_identity>() ?? other.GetComponent<player_identity>();
		if (pid == null) return;

		TeamRespawnCoordinator.Instance.MarkTouched(pid);
	}
}
