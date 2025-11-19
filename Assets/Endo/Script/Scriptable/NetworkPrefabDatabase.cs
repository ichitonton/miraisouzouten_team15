using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.PlayerLoop;

[CreateAssetMenu(fileName = "NetworkPrefabDatabase", menuName = "Scriptable Objects/NetworkPrefabDatabase")]
public class NetworkPrefabDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string _id;
        public GameObject _prefab;
    }

    public List<Entry> _entries = new List<Entry>();

    private Dictionary<string, GameObject> _keyToObjects;

    public void Initialize()
    {
        _keyToObjects = new Dictionary<string, GameObject>();
        foreach(var obj in _entries)
        {
            if(!_keyToObjects.ContainsKey(obj._id))
            {
                _keyToObjects.Add(obj._id,obj._prefab);
            }
        }
    }

    public GameObject GetPrefab(string id)
    {
        if (_keyToObjects == null) Initialize();

        if(_keyToObjects.TryGetValue(id, out var obj)) return obj;

        Debug.Log($"[PrefabDatabase] ID '{id}' ÇÕìoò^Ç≥ÇÍÇƒÇ¢Ç‹ÇπÇÒÅI");
        return null;
    }

}
