using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkVideoFadeMessenger : MonoBehaviour
{
    private const string MsgVideoFadeOut = "VIDEO_FADE_OUT";
    private const string MsgVideoFadeIn = "VIDEO_FADE_IN";

    [SerializeField] private VideoFadeOverlay overlay;

    private bool _registered = false;
    private CustomMessagingManager _cachedCmm = null;

    private void Awake()
    {
        if (overlay == null)
            overlay = FindFirstObjectByType<VideoFadeOverlay>(FindObjectsInactive.Include);

        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        _registered = false;
        _cachedCmm = null;
        TryRegister(force: true);
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (_registered) Unregister();
            return;
        }

        TryRegister(force: false);
    }

    private void TryRegister(bool force)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        bool changed = (_cachedCmm != cmm);
        if (!force && _registered && !changed) return;

        if (_registered && changed)
            Unregister();

        if (overlay == null)
            overlay = FindFirstObjectByType<VideoFadeOverlay>(FindObjectsInactive.Include);

        cmm.RegisterNamedMessageHandler(MsgVideoFadeOut, OnVideoFadeOut);
        cmm.RegisterNamedMessageHandler(MsgVideoFadeIn, OnVideoFadeIn);

        _cachedCmm = cmm;
        _registered = true;
    }

    private void Unregister()
    {
        try
        {
            if (_cachedCmm != null)
            {
                _cachedCmm.UnregisterNamedMessageHandler(MsgVideoFadeOut);
                _cachedCmm.UnregisterNamedMessageHandler(MsgVideoFadeIn);
            }
        }
        catch
        {
            // Shutdowníºå„Ç»Ç«Ç≈ì‡ïîÇ™éÄÇÒÇ≈ÇÈèÍçáÇ™Ç†ÇÈÇÃÇ≈à¨ÇËí◊Ç∑
        }

        _cachedCmm = null;
        _registered = false;
    }

    // =========================
    // Receive
    // =========================
    private void OnVideoFadeOut(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float outDuration);
        reader.ReadValueSafe(out int videoIndex);

        if (overlay == null)
            overlay = FindFirstObjectByType<VideoFadeOverlay>(FindObjectsInactive.Include);

        overlay?.PlayFadeOutOnly(outDuration, videoIndex);
    }

    private void OnVideoFadeIn(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float inDuration);
        reader.ReadValueSafe(out int videoIndex);

        if (overlay == null)
            overlay = FindFirstObjectByType<VideoFadeOverlay>(FindObjectsInactive.Include);

        overlay?.PlayFadeInOnly(inDuration, videoIndex);
    }

    // =========================
    // Send (Host)
    // =========================
    public void SendFadeOutToAll(float outDuration, int videoIndex)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        // float + int
        using var writer = new FastBufferWriter(sizeof(float) + sizeof(int), Allocator.Temp);
        writer.WriteValueSafe(outDuration);
        writer.WriteValueSafe(videoIndex);

        cmm.SendNamedMessageToAll(MsgVideoFadeOut, writer, NetworkDelivery.ReliableSequenced);
    }

    public void SendFadeInToAll(float inDuration, int videoIndex)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        using var writer = new FastBufferWriter(sizeof(float) + sizeof(int), Allocator.Temp);
        writer.WriteValueSafe(inDuration);
        writer.WriteValueSafe(videoIndex);

        cmm.SendNamedMessageToAll(MsgVideoFadeIn, writer, NetworkDelivery.ReliableSequenced);
    }
}
