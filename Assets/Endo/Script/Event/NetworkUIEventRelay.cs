using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkUIEventRelay : NetworkBehaviour
{
    public static NetworkUIEventRelay Instance { get; private set; }

    [Header("Database (All clients must have same asset)")]
    [SerializeField] private EventDatabase _eventDatabase;

    // 連続発火のガード（任意）
    [SerializeField] private float serverCooldown = 0.15f;
    private float _nextServerAllowedTime = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// どこから呼んでもOK。
    /// ネット無し: ローカル再生
    /// ネット有り: ClientならServerへ依頼、Serverなら即配信
    /// </summary>
    public void TriggerUIByEventId(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        // ネットワーク無し（ローカルデバッグ）
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            PlayLocal(eventId);
            return;
        }

        if (IsServer)
        {
            TriggerOnServer(eventId);
        }
        else
        {
            RequestTriggerServerRpc(new FixedString64Bytes(eventId));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerServerRpc(FixedString64Bytes eventId, ServerRpcParams rpcParams = default)
    {
        TriggerOnServer(eventId.ToString());
    }

    private void TriggerOnServer(string eventId)
    {
        if (!IsServer) return;

        if (Time.unscaledTime < _nextServerAllowedTime) return;
        _nextServerAllowedTime = Time.unscaledTime + serverCooldown;

        BroadcastClientRpc(new FixedString64Bytes(eventId));
    }

    [ClientRpc]
    private void BroadcastClientRpc(FixedString64Bytes eventId, ClientRpcParams rpcParams = default)
    {
        PlayLocal(eventId.ToString());
    }

    private void PlayLocal(string eventId)
    {
        if (_eventDatabase == null)
        {
            Debug.LogWarning($"[NetworkUIEventRelay] EventDatabaseが未設定です: {eventId}");
            return;
        }

        if (UIEventManager.Instance == null)
        {
            Debug.LogWarning($"[NetworkUIEventRelay] UIEventManagerが見つからないため表示できません: {eventId}");
            return;
        }

        // DBからUI演出情報を引く（SpriteなどはRPCで送らない！）
        var message = _eventDatabase.GetMessage(eventId);
        var icon = _eventDatabase.GetWarningIcon(eventId);
        var flash = _eventDatabase.GetFlash(eventId);

        UIEventManager.Instance.Play(message, icon, flash);
    }
}
