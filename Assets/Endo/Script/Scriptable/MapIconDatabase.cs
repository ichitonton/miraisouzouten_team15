using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapIconEntry
{
    [Header("ミニマップに出したいオブジェクトのPrefab")]
    public GameObject prefab;

    [Header("ミニマップ用アイコン")]
    public Sprite icon;
}

[CreateAssetMenu(fileName = "MapIconDatabase", menuName = "Scriptable Objects/MapIconDatabase")]
public class MapIconDatabase : ScriptableObject
{

    public List<MapIconEntry> _entries = new List<MapIconEntry>();
    // ランタイム用キャッシュ（名前 → アイコン）
    [NonSerialized]
    private Dictionary<string, Sprite> _iconTable;
    /// <summary>
    /// 初期化（必要になったタイミングで一度だけ）
    /// </summary>
    private void EnsureBuilt()
    {
        if (_iconTable != null) return;

        _iconTable = new Dictionary<string, Sprite>();

        foreach (var e in _entries)
        {
            if (e == null || e.prefab == null || e.icon == null) continue;

            string key = e.prefab.name; // Prefab名をそのままキーにする

            if (_iconTable.ContainsKey(key))
            {
                Debug.LogWarning($"[MapIconDatabase] 同じPrefab名「{key}」が複数登録されています。後からの登録で上書きします。");
            }

            _iconTable[key] = e.icon;
        }
    }

    // <summary>
    /// GameObject名から対応するアイコンを取得する
    /// </summary>
    public Sprite GetIconByObjectName(string objectName)
    {
        EnsureBuilt();

        if (string.IsNullOrEmpty(objectName) || _iconTable == null || _iconTable.Count == 0)
            return null;

        string cleaned = CleanName(objectName);

        // 1. 完全一致 (Prefab.name と同じ)
        if (_iconTable.TryGetValue(cleaned, out var exact))
        {
            return exact;
        }

        // 2. 部分一致 (Prefab名の一部が含まれていればOK)
        foreach (var kvp in _iconTable)
        {
            if (cleaned.Contains(kvp.Key))
                return kvp.Value;
        }

        // 3. 見つからなければ null
        return null;
    }

    private string CleanName(string name)
    {
        // 例: "WagashiBox(Clone)" → "WagashiBox"
        int idx = name.IndexOf('(');
        if (idx >= 0)
        {
            name = name.Substring(0, idx);
        }
        return name.Trim();
    }
}
