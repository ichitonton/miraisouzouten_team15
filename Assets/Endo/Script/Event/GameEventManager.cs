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

    public static GameEventManager Instance { get; private set; }

    private Coroutine _autoRoutine;
    private bool _isEventRunning; // ★イベント重複防止

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        TryStartAutoLoop();
    }

    private void Start()
    {
        // 非LANでも動作させたい場合の保険
        TryStartAutoLoop();
    }

    private void OnDisable()
    {
        StopAutoLoop();
    }

    private void TryStartAutoLoop()
    {
        if (!_autoRandomEvent) return;

        // LANならサーバーだけがイベントを決める
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) return;
        }

        if (_autoRoutine == null)
            _autoRoutine = StartCoroutine(AutoRandomEventLoop());
    }

    private void StopAutoLoop()
    {
        if (_autoRoutine != null)
        {
            StopCoroutine(_autoRoutine);
            _autoRoutine = null;
        }
    }

    private IEnumerator AutoRandomEventLoop()
    {
        while (true)
        {
            // イベント中なら終わるまで待つ
            while (_isEventRunning) yield return null;

            // DBが無い/空なら待機
            if (_eventDatabase == null || _eventDatabase.Entries == null || _eventDatabase.Entries.Count == 0)
            {
                yield return null;
                continue;
            }

            // ① 次の発火までランダム待機
            float min = Mathf.Max(0f, _minIntervalSec);
            float max = Mathf.Max(min + 0.01f, _maxIntervalSec);
            float wait = Random.Range(min, max);
            yield return new WaitForSeconds(wait);

            // 待機中に別ルートでイベントが始まってたら終わるまで待つ
            while (_isEventRunning) yield return null;

            // ② イベント発火（内部で eventTime を待つ）
            yield return StartCoroutine(SpawnRandomEventAndFire_BlockByEventTime());

            // ③ イベント終了後のクールダウン（必ず入れる 10～15秒）
            //   ※ここに来た時点で _isEventRunning は false に戻っている（finallyで解除）
            float cdMin = Mathf.Max(0f, _cooldownMinSec);
            float cdMax = Mathf.Max(cdMin + 0.01f, _cooldownMaxSec);
            float cooldown = Random.Range(cdMin, cdMax);
            yield return new WaitForSeconds(cooldown);

            // ループ先頭に戻って「またランダム待機→発火」へ
        }
    }

    private void OnGUI()
    {
        var w = 180;
        var h = 30;
        var x = Screen.width / 2 - w / 2;
        var y = (Screen.height / 2) - 100;

        if (GUI.Button(new Rect(x, y, w, h), "イベントスタート(即時)"))
        {
            // 即時も重複禁止（イベント中は無視）
            if (!_isEventRunning)
                StartCoroutine(SpawnRandomEventAndFire_BlockByEventTime());
            else
                Debug.Log("[GameEventManager] Event is running. Skip.");
        }

        if (GUI.Button(new Rect(x, y + 40, w, h), _autoRoutine == null ? "自動イベント ON" : "自動イベント OFF"))
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

    private IEnumerator SpawnRandomEventAndFire_BlockByEventTime()
    {
        // LANならサーバーのみ
        if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) yield break;
        }

        if (_isEventRunning) yield break; // 念のため
        _isEventRunning = true;

        try
        {
            var entries = _eventDatabase.Entries;
            if (entries == null || entries.Count == 0)
            {
                Debug.LogWarning("[GameEventManager] Database entries are empty.");
                yield break;
            }

            // ランダムで有効entryを選ぶ
            const int maxTry = 64;
            EventPrefabEntry chosen = null;

            int tryCount = Mathf.Min(maxTry, entries.Count);
            for (int i = 0; i < tryCount; i++)
            {
                var e = entries[Random.Range(0, entries.Count)];
                if (e != null && !string.IsNullOrWhiteSpace(e.eventId) && e.prefab != null)
                {
                    chosen = e;
                    break;
                }
            }

            if (chosen == null)
            {
                Debug.LogWarning("[GameEventManager] Could not find a valid entry (eventId/prefab).");
                yield break;
            }

            // Spawn
            Vector3 pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
            Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
            Transform parent = (_spawnAsChild && _spawnPoint != null) ? _spawnPoint : null;

            GameObject instance = Instantiate(chosen.prefab, pos, rot, parent);
            instance.name = $"{chosen.prefab.name}(Event:{chosen.eventId})";

            // LANなら NetworkObject.Spawn
            if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
            {
                var no = instance.GetComponent<NetworkObject>();
                if (no == null)
                {
                    Debug.LogError($"[GameEventManager] Prefab has no NetworkObject in LAN mode: {chosen.prefab.name}");
                    Destroy(instance);
                    yield break;
                }
                no.Spawn();
            }

            // EventBasic
            if (!instance.TryGetComponent<EventBasic>(out var ev))
            {
                Debug.LogWarning($"[GameEventManager] Spawned prefab has no EventBasic: {instance.name}");
                yield break;
            }

            Debug.Log($"[GameEventManager] Spawned '{chosen.message}' -> {instance.name}. Calling TryEvent()...");
            ev.TryEvent();

            // UI (LANならサーバーからRelay)
            if (GameManager.Instance != null && GameManager.Instance._IsLanModeActive)
                NetworkUIEventRelay.Instance?.TriggerUI(chosen.message);

            // ★イベント占有時間だけブロック
            float t = Mathf.Max(0f, ev._eventTime);
            if (t > 0f)
                yield return new WaitForSeconds(t);
        }
        finally
        {
            _isEventRunning = false;
        }
    }
}
