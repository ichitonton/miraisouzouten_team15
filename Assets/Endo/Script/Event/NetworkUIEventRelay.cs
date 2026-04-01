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

    // =========================================================
    // Single (existing)
    // =========================================================

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

        NetworkSoundManager.Instance.PlaySfx("UI_AlertJingle", NetworkSoundManager.SoundScope.LocalOnly, false);
    }

    // =========================================================
    // ★Double (new)
    // =========================================================

    /// <summary>
    /// ★同時イベント用UIトリガー
    /// - eventIdA / eventIdB: DB参照用（アイコン取得に使う）
    /// - combinedMessage: 上バナーに出す結合メッセージ（※Server側で作って渡すのが安全）
    /// - mergedFlash: 合成済みフラッシュ（Color/maxAlphaのみ使用。Spriteは送れないので無視される）
    /// </summary>
    public void TriggerUIDoubleByEventIds(string eventIdA, string eventIdB, string combinedMessage, ScreenFlashSetting mergedFlash)
    {
        if (string.IsNullOrWhiteSpace(eventIdA) || string.IsNullOrWhiteSpace(eventIdB))
            return;

        // ネットワーク無し（ローカルデバッグ）
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening)
        {
            PlayLocalDouble(eventIdA, eventIdB, combinedMessage, mergedFlash);
            return;
        }

        if (IsServer)
        {
            TriggerDoubleOnServer(eventIdA, eventIdB, combinedMessage, mergedFlash);
        }
        else
        {
            // クライアント→サーバーへ依頼（Spriteは送らない）
            var c = mergedFlash != null ? mergedFlash.flashColor : Color.clear;
            float a = mergedFlash != null ? Mathf.Clamp01(mergedFlash.maxAlpha) : 0f;

            RequestTriggerDoubleServerRpc(
                new FixedString64Bytes(eventIdA),
                new FixedString64Bytes(eventIdB),
                new FixedString128Bytes(combinedMessage ?? ""),
                new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f)
                )
            );
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerDoubleServerRpc(
        FixedString64Bytes eventIdA,
        FixedString64Bytes eventIdB,
        FixedString128Bytes combinedMessage,
        Color32 mergedFlashRGBA, // rgb = flashColor, a = maxAlpha(0-255)
        ServerRpcParams rpcParams = default)
    {
        // サーバー側で配信
        var color = new Color(
            mergedFlashRGBA.r / 255f,
            mergedFlashRGBA.g / 255f,
            mergedFlashRGBA.b / 255f,
            1f
        );
        float maxAlpha = mergedFlashRGBA.a / 255f;

        var flash = new ScreenFlashSetting
        {
            flashColor = color,
            maxAlpha = maxAlpha,
            overlaySprite = null // Spriteは送れない
        };

        TriggerDoubleOnServer(eventIdA.ToString(), eventIdB.ToString(), combinedMessage.ToString(), flash);
    }

    private void TriggerDoubleOnServer(string eventIdA, string eventIdB, string combinedMessage, ScreenFlashSetting mergedFlash)
    {
        if (!IsServer) return;

        if (Time.unscaledTime < _nextServerAllowedTime) return;
        _nextServerAllowedTime = Time.unscaledTime + serverCooldown;

        // RPCで送る値だけに変換（Sprite不可）
        var c = mergedFlash != null ? mergedFlash.flashColor : Color.clear;
        float a = mergedFlash != null ? Mathf.Clamp01(mergedFlash.maxAlpha) : 0f;

        var rgba = new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f)
        );

        BroadcastDoubleClientRpc(
            new FixedString64Bytes(eventIdA),
            new FixedString64Bytes(eventIdB),
            new FixedString128Bytes(combinedMessage ?? ""),
            rgba
        );
    }

    [ClientRpc]
    private void BroadcastDoubleClientRpc(
        FixedString64Bytes eventIdA,
        FixedString64Bytes eventIdB,
        FixedString128Bytes combinedMessage,
        Color32 mergedFlashRGBA,
        ClientRpcParams rpcParams = default)
    {
        var color = new Color(
            mergedFlashRGBA.r / 255f,
            mergedFlashRGBA.g / 255f,
            mergedFlashRGBA.b / 255f,
            1f
        );
        float maxAlpha = mergedFlashRGBA.a / 255f;

        var flash = new ScreenFlashSetting
        {
            flashColor = color,
            maxAlpha = maxAlpha,
            overlaySprite = null
        };

        PlayLocalDouble(eventIdA.ToString(), eventIdB.ToString(), combinedMessage.ToString(), flash);
    }

    private void PlayLocalDouble(string eventIdA, string eventIdB, string combinedMessage, ScreenFlashSetting mergedFlash)
    {
        if (_eventDatabase == null)
        {
            Debug.LogWarning($"[NetworkUIEventRelay] EventDatabaseが未設定です: {eventIdA}, {eventIdB}");
            return;
        }

        if (UIEventManager.Instance == null)
        {
            Debug.LogWarning($"[NetworkUIEventRelay] UIEventManagerが見つからないため表示できません: {eventIdA}, {eventIdB}");
            return;
        }

        // 同期表示：アイコンは各クライアントのDBから引く（SpriteをRPCで送らない）
        var iconA = _eventDatabase.GetWarningIcon(eventIdA);
        var iconB = _eventDatabase.GetWarningIcon(eventIdB);

        // バナー文はServerで作った combinedMessage をそのまま表示（全員一致させやすい）
        UIEventManager.Instance.PlayDouble(combinedMessage, iconA, iconB, mergedFlash);

        NetworkSoundManager.Instance.PlaySfx("UI_AlertJingle", NetworkSoundManager.SoundScope.LocalOnly, false);
    }
}
