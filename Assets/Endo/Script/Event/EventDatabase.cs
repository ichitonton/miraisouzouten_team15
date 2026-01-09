using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class EventPrefabEntry
{
    public string eventId = null;
    public GameObject prefab = null;
    public string message = "";
}

[CreateAssetMenu(fileName = "EventDatabase", menuName = "Scriptable Objects/EventDatabase")]
public class EventDatabase : ScriptableObject
{

    [SerializeField] private List<EventPrefabEntry> _entries = new();

    // ランタイム高速化用キャッシュ（必要になったら生成）
    private Dictionary<string, GameObject> _cache;

    public IReadOnlyList<EventPrefabEntry> Entries => _entries;

    /// <summary>
    /// eventId から Prefab を取得する（成功したら true）
    /// </summary>
    public bool TryGetPrefab(string eventId, out GameObject prefab)
    {
        prefab = null;

        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        BuildCacheIfNeeded();

        return _cache.TryGetValue(eventId, out prefab) && prefab != null;
    }

    public bool TryGetMessage(string eventId, out string message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        message = _entries.Find(e => e.eventId == eventId).message;

        GameObject prefab = null;

        return _cache.TryGetValue(eventId, out prefab) && prefab != null; ;
    }
    /// <summary>
    /// eventId から Prefab を取得する（見つからないと例外の代わりに null）
    /// </summary>
    public GameObject GetPrefabOrNull(string eventId)
    {
        return TryGetPrefab(eventId, out var prefab) ? prefab : null;
    }

    public string GetMessage(string eventId)
    {
        return TryGetMessage(eventId,out var message) ? message : null;
    }

    private void BuildCacheIfNeeded()
    {
        if (_cache != null) return;

        _cache = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        foreach (var e in _entries)
        {
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.eventId)) continue;
            if (e.prefab == null) continue;

            // 重複IDは「後勝ち」にする（事故りやすいなら Debug.LogWarning でもOK）
            _cache[e.eventId] = e.prefab;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // エディタでリスト触ったらキャッシュ破棄
        _cache = null;
    }
#endif

}
