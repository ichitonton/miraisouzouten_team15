using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string SceneName = string.Empty;

    // UI Button ‚Ì OnClick() ‚©‚çŒÄ‚Ô
    public void StartGame()
    {
        FadeManager.Instance.PlayToScene(SceneName, FadeManager.FadeScope.LocalOnly);
    }

    private void Update()
    {
        if (Gamepad.all.Count >= 0)
        {
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.rightShoulder.wasPressedThisFrame && gamepad.leftShoulder.wasPressedThisFrame)
                {
                    StartGame();
                }
            }
        }
    }
}

