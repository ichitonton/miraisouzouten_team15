using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GameStartMovie : MonoBehaviour
{
    public static GameStartMovie Instance { get; private set; }

    private VideoPlayer _videoPlayer = null;

    void Awake()
    {
        Instance = this;
        _videoPlayer = GetComponent<VideoPlayer>();
        _videoPlayer.loopPointReached += OnVideoEnd;
    }

    public void Play()
    {
        _videoPlayer.Play();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        gameObject.SetActive(false);
    }
}
