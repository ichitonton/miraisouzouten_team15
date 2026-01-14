using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class GameEventManager : NetworkBehaviour
{
    [Header("Database")]
    [SerializeField] private EventDatabase _eventDatabase;

    [Header("Spawn")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private bool _spawnAsChild = false;

    [Header("Auto Random Event (Seconds)")]
    [SerializeField] private bool _autoRandomEvent = true;

    [Tooltip("次イベントまでのランダム待機（秒）")]
    [SerializeField, Min(0f)] private float _minIntervalSec = 20f;

    [Tooltip("次イベントまでのランダム待機（秒）")]
    [SerializeField, Min(0.01f)] private float _maxIntervalSec = 60f;

    [Header("Cooldown After Event (Seconds)")]
    [Tooltip("イベント終了後に必ず入れるクールダウン（秒）")]
    [SerializeField, Min(0f)] private float _cooldownMinSec = 10f;

    [Tooltip("イベント終了後に必ず入れるクールダウン（秒）")]
    [SerializeField, Min(0.01f)] private float _cooldownMaxSec = 15f;

    [Header("Session Limit (Auto Loop)")]
    [Tooltip("自動イベントを何秒で終了するか（例：300 = 5分）")]
    [SerializeField, Min(1f)] private float _sessionDurationSec = 300f;

    [Tooltip("残りこの秒数になったら同時イベントを発生させる（例：60 = 残り1分）")]
    [SerializeField, Min(1f)] private float _doubleEventLastSec = 60f;

    [Header("Debug")]
    [SerializeField] private string _eventID = "none";

    public static GameEventManager Instance { get; private set; }

    private Coroutine _autoRoutine;
    private bool _isEventRunning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        // 必要ならここで自動開始もOK
        // TryStartAutoLoop();
    }

    private void Start()
    {
        // 非LANでも動作させたい場合の保険
        // TryStartAutoLoop();
    }

    private void OnDisable()
    {
        StopAutoLoop();
    }

    /// <summary>
    /// 自動ループ開始（外部から呼ぶ想定）
    /// </summary>
    public void TryStartAutoLoop()
    {
        if (!_autoRandomEvent) return;

        // LANならサーバーだけがイベントを決める
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) return;
        }

        if (_autoRoutine == null)
            _autoRoutine = StartCoroutine(AutoRandomEventLoop_TimeLimited());
    }

    private void StopAutoLoop()
    {
        if (_autoRoutine != null)
        {
            StopCoroutine(_autoRoutine);
            _autoRoutine = null;
        }
    }

    /// <summary>
    /// ★改良：自動イベントループはセッション時間で終了する
    /// - endTime まで動作
    /// - 残り1分（_doubleEventLastSec）に入ったら「同時イベント」モード
    /// </summary>
    private IEnumerator AutoRandomEventLoop_TimeLimited()
    {
        // DBが無い/空なら何もしない
        if (_eventDatabase == null || _eventDatabase.Entries == null || _eventDatabase.Entries.Count == 0)
        {
            Debug.LogWarning("[GameEventManager] Auto loop cannot start: Database empty.");
            _autoRoutine = null;
            yield break;
        }

        float startTime = Time.time;
        float endTime = startTime + Mathf.Max(1f, _sessionDurationSec);

        // ここを超えたら同時イベントを発火する
        float doubleStartTime = endTime - Mathf.Max(1f, _doubleEventLastSec);

        Debug.Log($"[GameEventManager] Auto loop start. endTime={endTime:F1}, doubleStart={doubleStartTime:F1}");

        while (Time.time < endTime)
        {
            // イベント中なら終わるまで待つ
            while (_isEventRunning) yield return null;

            // ① 次の発火までランダム待機（ただしセッション終了は超えない）
            float min = Mathf.Max(0f, _minIntervalSec);
            float max = Mathf.Max(min + 0.01f, _maxIntervalSec);
            float wait = Random.Range(min, max);

            float remain = endTime - Time.time;
            if (remain <= 0f) break;

            // 終了時刻が近いなら待機を短縮
            wait = Mathf.Min(wait, remain);
            yield return new WaitForSeconds(wait);

            if (Time.time >= endTime) break;

            // 待機中に別ルートでイベントが始まってたら終わるまで待つ
            while (_isEventRunning) yield return null;

            // ② イベント発火（残り1分なら同時）
            bool doubleMode = (Time.time >= doubleStartTime);

            if (doubleMode)
                yield return StartCoroutine(SpawnDoubleEventAndFire_BlockByMaxEventTime());
            else
                yield return StartCoroutine(SpawnRandomEventAndFire_BlockByEventTime());

            // ③ クールダウン（ただし終了時刻を超えない）
            float cdMin = Mathf.Max(0f, _cooldownMinSec);
            float cdMax = Mathf.Max(cdMin + 0.01f, _cooldownMaxSec);
            float cooldown = Random.Range(cdMin, cdMax);

            remain = endTime - Time.time;
            if (remain <= 0f) break;

            cooldown = Mathf.Min(cooldown, remain);
            yield return new WaitForSeconds(cooldown);
        }

        Debug.Log("[GameEventManager] Auto loop end.");
        _autoRoutine = null;
        _autoRandomEvent = false; // セッション終了したらOFFにしておく（好みで）
    }

    private void OnGUI()
    {
        var w = 220;
        var h = 30;
        var x = Screen.width / 2 - w / 2;
        var y = (Screen.height / 2) - 100;

        if (GUI.Button(new Rect(x, y, w, h), "イベントスタート(ランダム)"))
        {
            if (!_isEventRunning)
                StartCoroutine(SpawnRandomEventAndFire_BlockByEventTime());
            else
                Debug.Log("[GameEventManager] Event is running. Skip.");
        }

        if (GUI.Button(new Rect(x, y + 40, w, h), "イベントスタート(デバッグ用)"))
        {
            StartCoroutine(DebugEvent_BlockByEventTime(_eventID));
        }

        if (GUI.Button(new Rect(x, y + 80, w, h), _autoRoutine == null ? "自動イベント ON" : "自動イベント OFF"))
        {
            if (_autoRoutine == null)
            {
                _autoRandomEvent = true;
                TryStartAutoLoop();
            }
            else
            {
                _autoRandomEvent = false;
                StopAutoLoop();
            }
        }
    }

    // =========================================================
    // 単発イベント（既存）
    // =========================================================
    private IEnumerator SpawnRandomEventAndFire_BlockByEventTime()
    {
        // LANならサーバーのみ
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) yield break;
        }

        if (_isEventRunning) yield break;
        _isEventRunning = true;

        try
        {
            var chosen = PickRandomValidEntry(avoidId: null);
            if (chosen == null)
            {
                Debug.LogWarning("[GameEventManager] Could not find a valid entry.");
                yield break;
            }

            // Spawn
            if (!SpawnFromEntry(chosen, out var instance, out var ev))
                yield break;

            Debug.Log($"[GameEventManager] Spawned '{chosen.message}' -> {instance.name}. Calling TryEvent()...");
            ev.TryEvent();

            // UI（LANならサーバーからRelay）
            if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
            {
                NetworkUIEventRelay.Instance.TriggerUIByEventId(chosen.eventId);
            }
            else
            {
                UIEventManager.Instance?.Play(chosen.message, chosen.warningIcon, chosen.flash);
            }

            // eventTimeだけブロック
            float t = Mathf.Max(0f, ev._eventTime);
            if (t > 0f) yield return new WaitForSeconds(t);
        }
        finally
        {
            _isEventRunning = false;
        }
    }

    // =========================================================
    // ★新：同時イベント（2つ）
    // - eventTimeは長い方でブロック（重複防止）
    // =========================================================
    private IEnumerator SpawnDoubleEventAndFire_BlockByMaxEventTime()
    {
        // LANならサーバーのみ
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) yield break;
        }

        if (_isEventRunning) yield break;
        _isEventRunning = true;

        try
        {
            var a = PickRandomValidEntry(avoidId: null);
            var b = PickRandomValidEntry(avoidId: a != null ? a.eventId : null);

            if (a == null || b == null)
            {
                // 片方取れないなら単発にフォールバック
                if (a != null)
                    yield return StartCoroutine(SpawnSingleEntryFallback(a));
                else
                    Debug.LogWarning("[GameEventManager] Double event failed: no valid entries.");
                yield break;
            }

            // Spawn 2つ
            if (!SpawnFromEntry(a, out var instA, out var evA)) yield break;
            if (!SpawnFromEntry(b, out var instB, out var evB)) yield break;

            Debug.Log($"[GameEventManager] Double Spawn '{a.eventId}' & '{b.eventId}'");
            evA.TryEvent();
            evB.TryEvent();

            // UI：同時（メッセージ結合 + アイコン2つ + フラッシュ色合成）
            string msgA = a.message ?? "";
            string msgB = b.message ?? "";
            string combinedMsg = $"{msgA} / {msgB}";

            ScreenFlashSetting mergedFlash = MergeFlash(a.flash, b.flash);

            if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
            {
                // ★Relay側にこの関数を次で追加する想定
                // - LAN同期で “2イベント同時UI” を確実に出す
                NetworkUIEventRelay.Instance.TriggerUIDoubleByEventIds(
                    a.eventId, b.eventId, combinedMsg, mergedFlash
                );
            }
            else
            {
                UIEventManager.Instance?.PlayDouble(combinedMsg, a.warningIcon, b.warningIcon, mergedFlash);
            }

            // ブロックは長い方
            float t = Mathf.Max(Mathf.Max(0f, evA._eventTime), Mathf.Max(0f, evB._eventTime));
            if (t > 0f) yield return new WaitForSeconds(t);
        }
        finally
        {
            _isEventRunning = false;
        }
    }

    private IEnumerator SpawnSingleEntryFallback(EventPrefabEntry entry)
    {
        if (!SpawnFromEntry(entry, out var instance, out var ev)) yield break;

        ev.TryEvent();

        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
            NetworkUIEventRelay.Instance.TriggerUIByEventId(entry.eventId);
        else
            UIEventManager.Instance?.Play(entry.message, entry.warningIcon, entry.flash);

        float t = Mathf.Max(0f, ev._eventTime);
        if (t > 0f) yield return new WaitForSeconds(t);
    }

    // =========================================================
    // Debug（既存を維持：消さない）
    // =========================================================

    private bool IsServerAuthorityOK()
    {
        // LAN時はサーバーだけがイベントを決める
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
            return IsServer;

        // 非LANならローカルでOK
        return true;
    }

    private IEnumerator DebugEvent_BlockByEventTime(string id)
    {
        if (!IsServerAuthorityOK()) yield break;
        if (_isEventRunning) yield break;

        _isEventRunning = true;

        try
        {
            if (_eventDatabase == null)
            {
                Debug.LogWarning("[GameEventManager] EventDatabase is NULL.");
                yield break;
            }

            if (!_eventDatabase.TryGetEntry(id, out var entry) || entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[GameEventManager] Entry not found/prefab missing for id='{id}'");
                yield break;
            }

            if (!SpawnFromEntry(entry, out var instance, out var ev))
                yield break;

            Debug.Log($"[GameEventManager] Spawned debug '{id}' -> {instance.name}. Calling TryEvent()...");
            ev.TryEvent();

            // UI（デバッグもDBの内容に揃える）
            if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
                NetworkUIEventRelay.Instance.TriggerUIByEventId(id);
            else
                UIEventManager.Instance?.Play(entry.message, entry.warningIcon, entry.flash);

            float t = Mathf.Max(0f, ev._eventTime);
            if (t > 0f) yield return new WaitForSeconds(t);
        }
        finally
        {
            _isEventRunning = false;
        }
    }

    // =========================================================
    // Internal helpers
    // =========================================================

    private EventPrefabEntry PickRandomValidEntry(string avoidId)
    {
        var entries = _eventDatabase.Entries;
        if (entries == null || entries.Count == 0) return null;

        const int maxTry = 64;
        int tryCount = Mathf.Min(maxTry, entries.Count);

        for (int i = 0; i < tryCount; i++)
        {
            var e = entries[Random.Range(0, entries.Count)];
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.eventId)) continue;
            if (e.prefab == null) continue;
            if (!string.IsNullOrEmpty(avoidId) && e.eventId == avoidId) continue;
            return e;
        }

        // 念のため総当たり
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.eventId)) continue;
            if (e.prefab == null) continue;
            if (!string.IsNullOrEmpty(avoidId) && e.eventId == avoidId) continue;
            return e;
        }

        return null;
    }

    private bool SpawnFromEntry(EventPrefabEntry entry, out GameObject instance, out EventBasic ev)
    {
        instance = null;
        ev = null;

        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
        Transform parent = (_spawnAsChild && _spawnPoint != null) ? _spawnPoint : null;

        instance = Instantiate(entry.prefab, pos, rot, parent);
        instance.name = $"{entry.prefab.name}(Event:{entry.eventId})";

        // LANなら NetworkObject.Spawn
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            var no = instance.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError($"[GameEventManager] Prefab has no NetworkObject in LAN mode: {entry.prefab.name}");
                Destroy(instance);
                return false;
            }
            no.Spawn();
        }

        if (!instance.TryGetComponent<EventBasic>(out ev))
        {
            Debug.LogWarning($"[GameEventManager] Spawned prefab has no EventBasic: {instance.name}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 2イベントのフラッシュを合成（色は加算→clamp、alphaは強い方）
    /// </summary>
    private static ScreenFlashSetting MergeFlash(ScreenFlashSetting a, ScreenFlashSetting b)
    {
        if (a == null && b == null) return null;
        if (a == null) return CopyFlash(b);
        if (b == null) return CopyFlash(a);

        var m = new ScreenFlashSetting();

        Color ca = a.flashColor;
        Color cb = b.flashColor;

        m.flashColor = new Color(
            Mathf.Clamp01(ca.r + cb.r),
            Mathf.Clamp01(ca.g + cb.g),
            Mathf.Clamp01(ca.b + cb.b),
            1f
        );

        m.maxAlpha = Mathf.Clamp01(Mathf.Max(a.maxAlpha, b.maxAlpha));

        // overlaySprite は合成が難しいので null（必要ならルールを決めて片方採用）
        m.overlaySprite = null;

        return m;
    }

    private static ScreenFlashSetting CopyFlash(ScreenFlashSetting s)
    {
        if (s == null) return null;
        return new ScreenFlashSetting
        {
            flashColor = s.flashColor,
            maxAlpha = s.maxAlpha,
            overlaySprite = s.overlaySprite
        };
    }
}
