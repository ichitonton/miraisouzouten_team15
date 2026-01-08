using UnityEngine;
using Unity.Netcode;

public class GameEventManager : NetworkBehaviour
{
    [Header("Database")]
    [SerializeField] private EventDatabase _eventDatabase;

    [Header("Spawn")]
    [SerializeField] private Transform _spawnPoint;        // nullなら Vector3.zero
    [SerializeField] private bool _spawnAsChild = false;   // spawnPointの子にするか

    [SerializeField] private string _eventID = "none";

    public static GameEventManager Instance { get; private set; }

    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnGUI()
    {
       
        //if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) - 100, 120, 30), "イベントスタート"))
        //{
        //   SpawnRandomEventAndFire();
        //}

        if (GUI.Button(new Rect(Screen.width / 2 - 50, (Screen.height / 2) - 100, 120, 30), "イベントスタート"))
        {
            DebugEvent(_eventID);
        }
    }

    private void SpawnRandomEventAndFire()
    {
        //サーバー側でのみ実行
        if(GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) return;
        }
        

        var entries = _eventDatabase.Entries;
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[GameEventManager] Database entries are empty.");
            return;
        }

        // ランダムで「prefabが入っているもの」を引く（最大N回だけ試す）
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
            return;
        }

        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;

        Transform parent = (_spawnAsChild && _spawnPoint != null) ? _spawnPoint : null;

        GameObject instance = Instantiate(chosen.prefab, pos, rot, parent);

        //ネットワーク上で生成
        if(GameManager.Instance._IsLanModeActive)
        {
            instance.GetComponent<NetworkObject>().Spawn();
        }

        instance.name = $"{chosen.prefab.name}(Event:{chosen.eventId})";

        // 生成したprefabに EventBasic が付いてる前提で TryEvent
        if (instance.TryGetComponent<EventBasic>(out var ev))
        {
            // もしプレハブ側の _eventName をDBのIDで上書きしたいならここでやる
            // ただ _eventName は protected なので直接代入できない。
            // 必要なら EventBasic に SetEventName(string) を追加しよう（後で出す）。
            
            Debug.Log($"[GameEventManager] Spawned '{chosen.eventId}' -> {instance.name}. Calling TryEvent()...");
            ev.TryEvent();
        }
        else
        {
            Debug.LogWarning($"[GameEventManager] Spawned prefab has no EventBasic: {instance.name}");
        }
    }


    private void DebugEvent(string id)
    {

        //サーバー側でのみ実行
        if (GameManager.Instance._IsLanModeActive)
        {
            if (!IsServer) return;
        }

       
        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;

        Transform parent = (_spawnAsChild && _spawnPoint != null) ? _spawnPoint : null;

        //シリアライズフィールドからのIDのイベントを生成
        GameObject instance = Instantiate(_eventDatabase.GetPrefabOrNull(id), pos, rot, parent);

        //ネットワーク上で生成
        if (GameManager.Instance._IsLanModeActive)
        {
            instance.GetComponent<NetworkObject>().Spawn();
        }

        if ((instance.TryGetComponent<EventBasic>(out var ev)))
        {
            Debug.Log($"[GameEventManager] Spawned '{id}' -> {instance.name}. Calling TryEvent()...");
            ev.TryEvent();

            //メッセージを送信
            var message = _eventDatabase.GetMessage(id);

            //ネットワークでもUIを再生
            NetworkUIEventRelay.Instance.TriggerUI(message);
            //UIEventManager.Instance.Play(message);
            
        }
        else
        {
            Debug.LogWarning($"[GameEventManager] Spawned prefab has no EventBasic: {instance.name}");
        }
    }

}
