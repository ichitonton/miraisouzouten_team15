using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string SceneName = string.Empty;

    // UI Button ‚Ì OnClick() ‚©‚çŒÄ‚Ô
    public void StartGame()
    {
        FadeManager.Instance.PlayToScene(SceneName, FadeManager.FadeScope.LocalOnly);
    }

    
}
