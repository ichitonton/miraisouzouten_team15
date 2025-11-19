using UnityEngine;
using System;
using System.Collections.Generic;
using static NetworkSceneManager;

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Scriptable Objects/SceneDatabase")]
public class SceneDatabase : ScriptableObject
{

    [Serializable]
    public class SceneEntry
    {
        public SceneId id;       // —á: SceneId.Result
        public string sceneName; // —á: "ResultScene"
    }

    [SerializeField] private List<SceneEntry> entries = new();

    private Dictionary<SceneId, string> _lookup;

    void OnEnable()
    {
        BuildLookup();
    }

    //‰Šú‚ÌScene“o˜^
    void BuildLookup()
    {
        _lookup = new Dictionary<SceneId, string>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.sceneName)) continue;

            // d•¡ID‚ÍÅŒã‚Ì‚ğ—Dæi‚¨D‚İ‚ÅŒxo‚µ‚Ä‚àOKj
            _lookup[e.id] = e.sceneName;
        }
    }

    //enum‚É‘Î‰‚ÌScene‚Ì–¼‘O‚ğ•Ô‚·
    public bool TryGetSceneName(SceneId id, out string sceneName)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(id, out sceneName);
    }
}
