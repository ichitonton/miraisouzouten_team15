using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkFadeMessenger : MonoBehaviour
{
    private const string MsgFadeOut = "FADE_OUT";
    private const string MsgFadeIn = "FADE_IN";

    [SerializeField] private FadeOverlay fade;

    private bool _registered = false;

    //  CustomMessagingManager ‚ª•Ï‚í‚é‚±‚Æ‚ª‚ ‚é‚Ì‚ÅŠo‚¦‚Ä‚¨‚­
    private CustomMessagingManager _cachedCmm = null;

    private void Awake()
    {
        if (fade == null)
            fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        _registered = false;
        _cachedCmm = null;
        TryRegisterHandlers(force: true);
    }

    private void OnDisable()
    {
        UnregisterHandlers();
    }

    private void Update()
    {
        // –¢Ú‘±ó‘Ô‚È‚ç“o˜^‚ğƒŠƒZƒbƒgi2‰ñ–ÚˆÈ~‚Ì•œ‹A‚ÌŒ®j
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (_registered) UnregisterHandlers();
            return;
        }

        TryRegisterHandlers(force: false);
    }

    private void TryRegisterHandlers(bool force)
    {
        if (NetworkManager.Singleton == null) return;

        var cmm = NetworkManager.Singleton.CustomMessagingManager;
        if (cmm == null) return;

        //  2‰ñ–ÚˆÈ~A“à•”‚ÌCMM‚ª•Ï‚í‚Á‚Ä‚½‚çÄ“o˜^‚ª•K—v
        bool cmmChanged = (_cachedCmm != cmm);

        if (!force && _registered && !cmmChanged)
            return;

        // ‚à‚µ•Ï‚í‚Á‚Ä‚½‚çŒÃ‚¢‚à‚Ì‚ğ‰ğœ‚µ‚Ä‚©‚ç“o˜^‚µ’¼‚·
        if (_registered && cmmChanged)
            UnregisterHandlers();

        // fadeQÆ‚ª€‚ñ‚Å‚Ä‚à•œ‹A
        if (fade == null)
            fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        //  “o˜^
        cmm.RegisterNamedMessageHandler(MsgFadeOut, OnFadeOutMessage);
        cmm.RegisterNamedMessageHandler(MsgFadeIn, OnFadeInMessage);

        _cachedCmm = cmm;
        _registered = true;

        // Debug.Log("[NetworkFadeMessenger] Handlers registered.");
    }

    private void UnregisterHandlers()
    {
        // ‰ğœ‚Í—áŠO‚ªo‚é‚±‚Æ‚ª‚ ‚é‚Ì‚ÅƒK[ƒh‹­‚ß
        try
        {
            if (_cachedCmm != null)
            {
                _cachedCmm.UnregisterNamedMessageHandler(MsgFadeOut);
                _cachedCmm.UnregisterNamedMessageHandler(MsgFadeIn);
            }
        }
        catch
        {
            // Shutdown’¼Œã‚È‚Ç‚Å“à•”‚ª€‚ñ‚Å‚éê‡‚ª‚ ‚é‚Ì‚Åˆ¬‚è‚Â‚Ô‚µ
        }

        _cachedCmm = null;
        _registered = false;

        // Debug.Log("[NetworkFadeMessenger] Handlers unregistered.");
    }

    private void OnFadeOutMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float duration);

        if (fade == null)
            fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        fade?.FadeOut(duration);
    }

    private void OnFadeInMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out float duration);

        if (fade == null)
            fade = FindFirstObjectByType<FadeOverlay>(FindObjectsInactive.Include);

        fade?.FadeIn(duration);
    }

    // =========================
    // Host‚ªŒÄ‚ÔF‘Sˆõ‚Ö‡}
    // =========================
    public void SendFadeOutToAll(float duration)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        using var writer = new FastBufferWriter(sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(duration);

        // Reliable‚É‚µ‚Äæ‚è‚±‚Ú‚µ–h~i2‰ñ–ÚˆÈ~‚àˆÀ’èj
        cmm.SendNamedMessageToAll(MsgFadeOut, writer, NetworkDelivery.ReliableSequenced);
    }

    public void SendFadeInToAll(float duration)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        using var writer = new FastBufferWriter(sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(duration);

        cmm.SendNamedMessageToAll(MsgFadeIn, writer, NetworkDelivery.ReliableSequenced);
    }
}
