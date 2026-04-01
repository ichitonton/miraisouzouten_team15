using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NetworkPrefabDatabase", menuName = "Scriptable Objects/NetworkPrefabDatabase")]
public class NetworkPrefabDatabase : ScriptableObject
{
    // Inspectorで選ばせる用（表示が分かりやすい）
    public enum RarityTier
    {
        Rarity1 = 1,
        Rarity2 = 2,
        Rarity3 = 3
    }

    [Serializable]
    public class Entry
    {
        public string _id;
        public GameObject _prefab;

        // 実データは int のまま（要件通り）
        private int _rarity = 1;

        // Inspector表示用（これを触る）
        [SerializeField] private RarityTier _rarityEnum = RarityTier.Rarity1;

        public int Rarity => _rarity;

        // Inspector変更やリロード時に整合性を取る
        public void SyncRarity()
        {
            // Enum -> int
            _rarity = (int)_rarityEnum;

            // 念のためクランプ
            _rarity = Mathf.Clamp(_rarity, 1, 3);

            // int -> Enum（壊れた値が入っても戻す）
            _rarityEnum = (RarityTier)_rarity;
        }
    }

    public List<Entry> _entries = new List<Entry>();

    private Dictionary<string, Entry> _idToEntry;

    private void OnEnable()
    {
        Initialize();
    }

    private void OnValidate()
    {
        // ScriptableObjectをInspectorで触った時に同期
        if (_entries == null) return;
        foreach (var e in _entries)
        {
            if (e == null) continue;
            e.SyncRarity();
        }
    }

    public void Initialize()
    {
        _idToEntry = new Dictionary<string, Entry>();

        foreach (var e in _entries)
        {
            if (e == null) continue;

            e.SyncRarity();

            if (string.IsNullOrWhiteSpace(e._id))
            {
                Debug.LogWarning("[NetworkPrefabDatabase] 空のIDが含まれています。スキップします。");
                continue;
            }

            if (_idToEntry.ContainsKey(e._id))
            {
                Debug.LogWarning($"[NetworkPrefabDatabase] ID '{e._id}' が重複しています。最初の登録を優先します。");
                continue;
            }

            _idToEntry.Add(e._id, e);
        }
    }

    private bool EnsureInitialized()
    {
        if (_idToEntry != null) return true;
        Initialize();
        return _idToEntry != null;
    }

    public GameObject GetPrefab(string id)
    {
        if (!EnsureInitialized()) return null;

        if (_idToEntry.TryGetValue(id, out var entry) && entry != null)
            return entry._prefab;

        Debug.Log($"[PrefabDatabase] ID '{id}' は登録されていません！");
        return null;
    }

    public int GetPrefabRarity(string id)
    {
        if (!EnsureInitialized()) return 0;

        if (_idToEntry.TryGetValue(id, out var entry) && entry != null)
            return entry.Rarity;

        Debug.Log($"[PrefabDatabase] ID '{id}' は登録されていません！（Rarity取得失敗）");
        return 0;
    }

    public bool TryGetPrefabAndRarity(string id, out GameObject prefab, out int rarity)
    {
        prefab = null;
        rarity = 0;

        if (!EnsureInitialized()) return false;

        if (_idToEntry.TryGetValue(id, out var entry) && entry != null)
        {
            prefab = entry._prefab;
            rarity = entry.Rarity;
            return true;
        }

        return false;
    }
}
