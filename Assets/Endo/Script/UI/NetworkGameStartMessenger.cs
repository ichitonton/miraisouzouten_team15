using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameStartMessenger : MonoBehaviour
{
    private const string MsgGameStartScheduled = "GAME_START_SCHEDULED";

    private bool _registered = false;
    private CustomMessagingManager _cachedCmm = null;

    private void Awake()
    {
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
        //  未接続なら解除して次の再接続に備える
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (_registered) UnregisterHandlers();
            return;
        }

        TryRegisterHandlers(force: false);
    }

    private void TryRegisterHandlers(bool force)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        bool changed = (_cachedCmm != cmm);

        if (!force && _registered && !changed) return;

        if (_registered && changed)
            UnregisterHandlers();

        cmm.RegisterNamedMessageHandler(MsgGameStartScheduled, OnGameStartScheduledMessage);

        _cachedCmm = cmm;
        _registered = true;
    }

    private void UnregisterHandlers()
    {
        try
        {
            if (_cachedCmm != null)
                _cachedCmm.UnregisterNamedMessageHandler(MsgGameStartScheduled);
        }
        catch { }

        _cachedCmm = null;
        _registered = false;
    }

    private void OnGameStartScheduledMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out double startServerTime);

        //  予定時刻まで待って再生（完全一致）
        GameStartUIManager.Instance?.PlayAtServerTime(startServerTime);
    }

    /// <summary>
    ///  Hostが呼ぶ：開始予定時刻を全員へ送信
    /// </summary>
    public void SendStartToAll(double startServerTime)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;
        if (!nm.IsServer) return;

        var cmm = nm.CustomMessagingManager;
        if (cmm == null) return;

        using var writer = new FastBufferWriter(sizeof(double), Allocator.Temp);
        writer.WriteValueSafe(startServerTime);

        cmm.SendNamedMessageToAll(
            MsgGameStartScheduled,
            writer,
            NetworkDelivery.ReliableSequenced
        );
    }
}
