using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TeamRespawnCoordinator : MonoBehaviour
{
	public static TeamRespawnCoordinator Instance { get; private set; }

	[Header("���X�|�[���܂ł̑ҋ@(�b)")]
	[SerializeField, Min(0f)] float respawnDelay = 0f;

	// teamId �� �G�ꂽ�����o�[ID�W���i1,2�j
	readonly Dictionary<int, HashSet<int>> touched = new();

	void Awake()
	{
		if (Instance && Instance != this) { Destroy(gameObject); return; }
		Instance = this;
	}

	// ���X�|�[���G���A����Ă΂��F���̃v���C���[�� True �ɂ���
	public void MarkTouched(player_identity pid)
	{
		if (!touched.TryGetValue(pid.teamId, out var set))
		{
			set = new HashSet<int>();
			touched[pid.teamId] = set;
		}
		set.Add(pid.memberId);

		// �`�[������ 1 �� 2 ����������甭��
		if (set.Contains(1) && set.Contains(2))
		{
			StartCoroutine(RespawnTeam(pid.teamId));
		}
	}

	IEnumerator RespawnTeam(int teamId)
	{
		if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);

		// �`�[������ 1 �� 2 ��T���i��A�N�e�B�u���O�j
		var players = FindObjectsByType<player_identity>(
			FindObjectsInactive.Exclude, FindObjectsSortMode.None);

		foreach (var p in players)
		{
			if (p.teamId != teamId) continue;
			if (p.memberId != 1 && p.memberId != 2) continue;

			// ���x���Z�b�g
			var rb = p.GetComponent<Rigidbody>();
			if (rb)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}

			// ���ĂсiSendMessage��߁j
			// �v���C���[���� public void SetReset() ������O��
			p.SetReset();
		}

		// ��ԃN���A�i�����E���h�p�j
		touched.Remove(teamId);
	}
}
