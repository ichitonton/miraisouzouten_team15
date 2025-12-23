using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectDatabase", menuName = "Scriptable Objects/EffectDatabase")]
public class EffectDatabase : ScriptableObject
{
    [Serializable]
    public class EffectEntry
    {
        public int effectId;          // エフェクト識別用ID（自分で決める）
        public string label;          // インスペクタで分かりやすくする用
        public GameObject prefab;     // 再生したいパーティクルなど
    }

    [SerializeField]
    private List<EffectEntry> _effects = new List<EffectEntry>();

    public IReadOnlyList<EffectEntry> Effects => _effects; // ★追加
    /// <summary>
    /// ID から対応するエフェクトPrefabを取得
    /// </summary>
    public GameObject GetEffectPrefab(int id)
    {
        foreach (var e in _effects)
        {
            if (e.effectId == id)
            {
                return e.prefab;
            }
        }

        Debug.LogWarning($"[EffectDatabase] effectId={id} に対応するPrefabが見つかりません");
        return null;
    }

    /// <summary>
    /// ID から対応するエフェクトPrefabを取得
    /// </summary>
    public GameObject GetEffectPrefab(string label)
    {
        foreach (var e in _effects)
        {
            if (e.label == label)
            {
                return e.prefab;
            }
        }

        Debug.LogWarning($"[EffectDatabase] effectId={label} に対応するPrefabが見つかりません");
        return null;
    }

    // ========== IDを取得（Key → ID） ==========
    public int GetEffectId(string key)
    {
        foreach (var e in _effects)
        {
            if (e.label == key)
            {
                return e.effectId;
            }
        }

        Debug.LogWarning($"[EffectDatabase] effectKey=\"{key}\" に対応する effectId がありません");
        return -1;
    }

    // ========== Key を取得（ID → Key） ==========
    public string GetEffectKey(int id)
    {
        foreach (var e in _effects)
        {
            if (e.effectId == id)
            {
                return e.label;
            }
        }

        Debug.LogWarning($"[EffectDatabase] effectId={id} に対応する key がありません");
        return null;
    }

}
