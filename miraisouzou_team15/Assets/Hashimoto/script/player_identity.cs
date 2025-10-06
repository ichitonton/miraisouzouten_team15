using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class player_identity : MonoBehaviour
{
	[Min(0)] public int teamId = 0;
	[Range(1, 2)] public int memberId = 1;

	[Header("任意: リスポーン位置（未指定なら初期位置）")]
	[SerializeField] Transform respawnPoint;

	Vector3 startPos;
	Quaternion startRot;
	Rigidbody rb;

	void Start()
	{
		startPos = transform.position;
		startRot = transform.rotation;
		rb = GetComponent<Rigidbody>();
	}

	// TeamRespawnCoordinator から SendMessage で呼ばれる想定
	void SetReset()
	{
		var pos = respawnPoint ? respawnPoint.position : startPos;
		var rot = respawnPoint ? respawnPoint.rotation : startRot;

		transform.SetPositionAndRotation(pos, rot);

		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}
	}
}
