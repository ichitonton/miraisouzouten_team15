using UnityEngine;

public class player_identity : MonoBehaviour

{
	[Header("チーム識別子（0=A,1=B…）")]
	[Min(0)] public int teamId = 0;

	[Header("チーム内の番号（1 or 2）")]
	[Range(1, 2)] public int memberId = 1;

	// 将来的に拡張したいならここに追加できる
	// public string playerName;
	// public Color teamColor;
}

