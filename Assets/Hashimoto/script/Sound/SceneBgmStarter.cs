using UnityEngine;

public class SceneBgmStarter : MonoBehaviour
{
	[SerializeField] private string bgmTag = "Taiki";

	private void Start()
	{
		if (NetworkSoundManager.Instance == null) return;

		NetworkSoundManager.Instance.PlayBgm(
			bgmTag,
			NetworkSoundManager.SoundScope.LocalOnly
		);
	}
}
