using System.Collections;
using System.Xml.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoTransitionUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage rawImage;

    [Header("Material (UI/VideoKeyBlack)")]
    [SerializeField] private Material keyMaterial;

    [Header("Fade")]
    [SerializeField] private float fadeOutTail = 0.15f; // 最後を少し薄くして消す

    static readonly int GlobalAlphaId = Shader.PropertyToID("_GlobalAlpha");

    private void Awake()
    {
        if (rawImage != null && keyMaterial != null)
            rawImage.material = keyMaterial;

        SetAlpha(0f);
        rawImage.gameObject.SetActive(false);
    }

    private void OnGUI()
    {
        // 既存デバッグGUIは残す（LAN中ホストのみ）
        
        if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) + 200, 120, 30), "tらんじしょん"))
        {
            PlayTransition();
        }
    }

    void SetAlpha(float a)
    {
        if (keyMaterial != null)
            keyMaterial.SetFloat(GlobalAlphaId, a);
    }

    public void PlayTransition()
    {
        StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        rawImage.gameObject.SetActive(true);

        // 黒抜きなので最初から見えてもOK（黒は透明）
        SetAlpha(1f);

        videoPlayer.Stop();
        videoPlayer.Play();

        // 再生終了待ち
        while (videoPlayer.isPlaying)
            yield return null;

        // ちょいフェードで消す（任意）
        float t = 0f;
        while (t < fadeOutTail)
        {
            t += Time.unscaledDeltaTime;
            //SetAlpha(Mathf.Lerp(1f, 0f, t / fadeOutTail));
            yield return null;
        }

        //SetAlpha(0f);
        rawImage.gameObject.SetActive(false);
    }
}
