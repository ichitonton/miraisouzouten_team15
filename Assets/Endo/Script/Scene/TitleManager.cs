using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static UnityEngine.EventSystems.StandaloneInputModule;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string SceneName = string.Empty;

    // UI Button の OnClick() から呼ぶ
    public void StartGame()
    {
        VideoFadeManager.Instance.PlayToScene(SceneName, VideoFadeManager.FadeScope.LocalOnly);
    }

	private void Update()
	{
		bool requestedStart = false;

		// キーボード（Enter）
		if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
		{
			requestedStart = true;
		}

		// ゲームパッド（2台想定：どっちかがY押したらOK）
		if (!requestedStart && Gamepad.all.Count > 0)
		{
			foreach (var pad in Gamepad.all)
			{
				if (pad != null && pad.buttonWest.wasPressedThisFrame) // Yボタン
				{
					requestedStart = true;
					break;
				}
			}
		}

		if (requestedStart)
		{
			StartGame();
		}
	}

}

