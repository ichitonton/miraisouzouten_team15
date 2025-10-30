using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class player_identity : MonoBehaviour
{
	[Min(0)] public int teamId = 0;
	[Range(1, 2)] public int memberId = 1;

	[Header("�C��: ���X�|�[���ʒu�i���w��Ȃ珉���ʒu�j")]
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

	// TeamRespawnCoordinator ���� SendMessage �ŌĂ΂��z��
	public void SetReset()
	{
		var pos = respawnPoint ? respawnPoint.position : startPos;
		var rot = respawnPoint ? respawnPoint.rotation : startRot;

		transform.SetPositionAndRotation(pos, rot);

		if (rb != null)
		{
			rb.linearVelocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}
	}
}
