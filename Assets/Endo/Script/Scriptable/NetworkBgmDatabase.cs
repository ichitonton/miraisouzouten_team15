using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "NetworkBgmDatabase", menuName = "Scriptable Objects/NetworkBgmDatabase")]
public class NetworkBgmDatabase : ScriptableObject
{
    [Serializable]
    public class BgmEntry
    {
        [Tooltip("呼び出し用キー。例: GameStart, Result, Lobby")]
        public string tag;

        [Tooltip("同じtagに複数入れるとランダムで選曲できる")]
        public List<AudioClip> clips = new();

        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;

        [Tooltip("BGM切り替え時のフェード秒数（0なら即切り替え）")]
        [Min(0f)] public float fadeSeconds = 0.2f;

        [Tooltip("任意: AudioMixerGroup を使うなら設定")]
        public AudioMixerGroup outputMixerGroup;
    }

    [SerializeField] private List<BgmEntry> entries = new();

    public bool TryGet(string tag, out BgmEntry entry)
    {
        if (string.IsNullOrEmpty(tag))
        {
            entry = null;
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].tag == tag)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = null;
        return false;
    }

    public AudioClip GetRandomClip(BgmEntry entry, System.Random rng)
    {
        if (entry == null || entry.clips == null || entry.clips.Count == 0) return null;
        int idx = rng.Next(0, entry.clips.Count);
        return entry.clips[idx];
    }
}
