using UnityEngine;

[RequireComponent(typeof(Collider))]
public class respone_player : MonoBehaviour
{
	[SerializeField] string playerTag = "Player";

	void Reset()
	{
		var col = GetComponent<Collider>();
		col.isTrigger = true;
		// プレイヤー側にRigidbodyが無いなら、ゾーン側に Kinematic Rigidbody を付けると確実
		// var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
	}

	void OnTriggerEnter(Collider other)
	{
		if (!other.CompareTag(playerTag)) return;

		var pid = other.GetComponentInParent<player_identity>() ?? other.GetComponent<player_identity>();
		if (!pid) return;

		TeamRespawnCoordinator.Instance.MarkTouched(pid);
	}
}
