using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkFadeMessenger : MonoBehaviour
{
    const string MsgFadeOut = "FADE_OUT";
    const string MsgFadeIn = "FADE_IN";

    [SerializeField] private FadeOverlay fade;

    bool _registered;

    private void Awake()
    {
        if (fade == null) fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => TryRegisterHandlers();
    private void Update() => TryRegisterHandlers();

    void TryRegisterHandlers()
    {
        if (_registered) return;
        if (NetworkManager.Singleton == null) return;

        var cmm = NetworkManager.Singleton.CustomMessagingManager;
        if (cmm == null) return;

        cmm.RegisterNamedMessageHandler(MsgFadeOut, OnFadeOutMessage);
        cmm.RegisterNamedMessageHandler(MsgFadeIn, OnFadeInMessage);

        _registered = true;
    }

    void OnFadeOutMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float duration);
        if (fade == null) fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);
        fade?.FadeOut(duration);
    }

    void OnFadeInMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float duration);
        if (fade == null) fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);
        fade?.FadeIn(duration);
    }

    // ホストが呼ぶ：全員に合図
    public void SendFadeOutToAll(float duration)
    {
        if (NetworkManager.Singleton == null) return;
        using var writer = new FastBufferWriter(sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(duration);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MsgFadeOut, writer);
    }

    public void SendFadeInToAll(float duration)
    {
        if (NetworkManager.Singleton == null) return;
        using var writer = new FastBufferWriter(sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(duration);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(MsgFadeIn, writer);
    }
}
