using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TeamRespawnCoordinator : MonoBehaviour
{
	public static TeamRespawnCoordinator Instance { get; private set; }

	[Header("リスポーンまでの待機(秒)")]
	[SerializeField, Min(0f)] float respawnDelay = 0f;

	// teamId → 触れたメンバーID集合（1,2）
	readonly Dictionary<int, HashSet<int>> touched = new();

	void Awake()
	{
		if (Instance && Instance != this) { Destroy(gameObject); return; }
		Instance = this;
	}

	// リスポーンエリアから呼ばれる：このプレイヤーを True にする
	public void MarkTouched(player_identity pid)
	{
		if (!touched.TryGetValue(pid.teamId, out var set))
		{
			set = new HashSet<int>();
			touched[pid.teamId] = set;
		}
		set.Add(pid.memberId);

		// チーム内の 1 と 2 がそろったら発火
		if (set.Contains(1) && set.Contains(2))
		{
			StartCoroutine(RespawnTeam(pid.teamId));
		}
	}

	IEnumerator RespawnTeam(int teamId)
	{
		if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);

		// 同じチームの 1 と 2 を探して SetReset() 実行
		var all = GameObject.FindObjectsOfType<player_identity>();
		foreach (var p in all)
		{
			if (p.teamId != teamId) continue;
			if (p.memberId != 1 && p.memberId != 2) continue;

			// 速度止めたいならここで Rigidbody をゼロにしてOK
			p.gameObject.SendMessage("SetReset", SendMessageOptions.DontRequireReceiver);
			p.gameObject.SendMessageUpwards("SetReset", SendMessageOptions.DontRequireReceiver);
		}

		// 状態クリア（次のラウンド用）
		touched.Remove(teamId);
	}
}
