using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static UnityEngine.EventSystems.StandaloneInputModule;

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

        if (Input.GetKey(KeyCode.Return))
        {
            StartGame();
        }

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

