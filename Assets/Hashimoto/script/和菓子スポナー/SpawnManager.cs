using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
	[Header("数の制限")]
	[SerializeField] int maxSweets = 18;
	[SerializeField] int minSweets = 10;

	[Header("スポーン設定")]
	[SerializeField] float respawnDelay = 1.0f; // 消滅から再生成までの遅延（秒）
	[SerializeField] Sponer_Wagashi[] spawners;

	private List<GameObject> objList;

	void Start()
	{
		GameObject[] objs = GameObject.FindGameObjectsWithTag("Sweets");
		objList = new List<GameObject>(objs);
		Debug.LogWarning($"[SpawnMgr] Init: alive={objs.Length} / list={objList.Count}");
	}

	public void DestroySweets(GameObject sweets)
	{
		Debug.Log(sweets);
		if (!objList.Contains(sweets)) return;
		objList.Remove(sweets);

		StartCoroutine(RespawnAfterDelay(respawnDelay));
	}

	private IEnumerator RespawnAfterDelay(float delay)
	{
		yield return new WaitForSeconds(delay);

		int nowCount = objList.Count;
		float probability = GetSpawnProbability(nowCount);
		float randomValue = Random.value;

		if (randomValue < probability && nowCount < maxSweets)
		{
			int val = Random.Range(0, spawners.Length);
			GameObject work = spawners[val].Spawn();
			objList.Add(work);

			Debug.LogWarning($"[Respawn ✅] Count={nowCount + 1}, Delay={delay:F1}s, Prob={probability:P0}, Spawner={val}");
		}
		else
		{
			Debug.LogWarning($"[No Respawn ❌] Count={nowCount}, Delay={delay:F1}s, Prob={probability:P0}");
		}
	}

	// 現在数に応じたスポーン確率（0〜1）
	float GetSpawnProbability(int currentCount)
	{
		if (currentCount <= minSweets)
			return 1f;
		if (currentCount >= maxSweets)
			return 0f;

		float t = Mathf.InverseLerp(maxSweets, minSweets, currentCount);
		return Mathf.Clamp01(t);
	}
}
