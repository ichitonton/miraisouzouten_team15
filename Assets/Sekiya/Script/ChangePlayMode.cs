using UnityEngine;

public class ChangePlayMode : MonoBehaviour
{
	[Header("UI Images")]
	public GameObject hostImage;
	public GameObject clientImage;

	[Header("SE")]
	[SerializeField] private AudioSource seSource;
	[SerializeField] private AudioClip seSwitch;
	[SerializeField] private AudioClip seDecision;

	private bool isHostMode = false;

	void Start()
	{
		UpdateUI();
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
		{
			isHostMode = !isHostMode;
			UpdateUI();
			Debug.Log("現在のモード: " + (isHostMode ? "HOST" : "CLIENT"));

			// モード切替SE
			if (seSource != null && seSwitch != null)
				seSource.PlayOneShot(seSwitch);
		}

		if (Input.GetKeyDown(KeyCode.Return))
		{
			StartGame();

			// 決定SE
			if (seSource != null && seDecision != null)
				seSource.PlayOneShot(seDecision);
		}
	}

	void UpdateUI()
	{
		// ここがnullだと死ぬから、未設定なら落ちる（注意）
		if (isHostMode)
		{
			hostImage.SetActive(true);
			clientImage.SetActive(false);
		}
		else
		{
			hostImage.SetActive(false);
			clientImage.SetActive(true);
		}
	}

	void StartGame()
	{
		if (isHostMode)
		{
			Debug.Log("ホストとしてゲーム開始！");
		}
		else
		{
			Debug.Log("クライアントとしてゲーム開始！");
		}
	}
}
