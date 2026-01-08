using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkUIEventRelay : NetworkBehaviour
{
    public static NetworkUIEventRelay Instance { get; private set; }

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
    /// どこから呼んでもOK。ClientならServerに依頼、Serverなら即配信。
    /// </summary>
    public void TriggerUI(string message)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            // ネットワーク無し（ローカルデバッグ）
            UIEventManager.Instance?.Play(message);
            return;
        }

        if (IsServer)
        {
            TriggerOnServer(message);
        }
        else
        {
            // Serverに「これを全員に出して」と依頼
            RequestTriggerServerRpc(new FixedString128Bytes(message));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerServerRpc(FixedString128Bytes message, ServerRpcParams rpcParams = default)
    {
        TriggerOnServer(message.ToString());
    }

    private void TriggerOnServer(string message)
    {
        if (!IsServer) return;

        if (Time.unscaledTime < _nextServerAllowedTime) return;
        _nextServerAllowedTime = Time.unscaledTime + serverCooldown;

        BroadcastClientRpc(new FixedString128Bytes(message));
    }

    [ClientRpc]
    private void BroadcastClientRpc(FixedString128Bytes message, ClientRpcParams rpcParams = default)
    {
        // 各クライアントのローカルCanvasで演出再生
        if (UIEventManager.Instance != null)
        {
            UIEventManager.Instance.Play(message.ToString());
        }
        else
        {
            Debug.LogWarning($"[NetworkUIEventRelay] UIEventManagerが見つからないため表示できません: {message}");
        }
    }
}
