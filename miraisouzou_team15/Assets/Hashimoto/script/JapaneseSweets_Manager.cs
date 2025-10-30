using UnityEngine;

public class JapaneseSweets_Manager : MonoBehaviour
{
	[SerializeField] private float weight = 0.0f; // オブジェクトの重さ
	private SpawnManager spawnManager;            // 破棄通知先

	private void Awake()
	{
		// シーン内から SpawnManager を自動で探す
		spawnManager = FindFirstObjectByType<SpawnManager>();
	}

	public float GetWeight()
	{
		return weight;
	}

	private void OnDestroy()
	{
		if (!Application.isPlaying) return;

		// 既にシーン終了中なら何もしない
		if (this == null) return;

		// SpawnManager を探して存在する場合だけ呼ぶ
		var spawnManager = FindFirstObjectByType<SpawnManager>();
		if (spawnManager != null)
		{
			spawnManager.DestroySweets(gameObject);
		}
	}

}
