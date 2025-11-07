using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangerWithSoundInvoke : MonoBehaviour
{
    [SerializeField] private AudioClip soundEffect;
    [SerializeField] private string nextSceneName;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // ボタンなどから呼び出す
    public void PlayAndChangeScene()
    {
        // 効果音を再生
        audioSource.PlayOneShot(soundEffect);

        // 効果音の長さ（秒）後に、LoadNextSceneメソッドを呼び出す
        Invoke("LoadNextScene", soundEffect.length);
    }

    public void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}