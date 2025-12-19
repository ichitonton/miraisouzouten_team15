using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "NetworkSeDatabase", menuName = "Scriptable Objects/NetworkSeDatabase")]
public class NetworkSeDatabase : ScriptableObject
{
    [Serializable]
    public class SfxEntry
    {
        [Tooltip("呼び出し用キー。例: CannonHit, CannonShoot, UI_Click")]
        public string tag;

        [Tooltip("同じtagに複数入れるとランダムでバリエーション再生")]
        public List<AudioClip> clips = new();

        [Range(0f, 1f)] public float volume = 1f;

        [Header("Pitch Random")]
        public bool randomizePitch = true;
        [Min(0f)] public float pitchMin = 0.95f;
        [Min(0f)] public float pitchMax = 1.05f;

        [Header("3D Settings (when spatial=true)")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0f)] public float minDistance = 3f;
        [Min(0f)] public float maxDistance = 35f;

        [Header("Anti-Spam")]
        [Tooltip("同じtagを連打した時に鳴らす頻度を抑える（秒）")]
        [Min(0f)] public float cooldownSeconds = 0.03f;

        [Tooltip("同時に鳴らせる最大数（簡易制限）。0なら制限なし")]
        [Min(0)] public int maxSimultaneous = 0;

        [Tooltip("任意: AudioMixerGroup を使うなら設定")]
        public AudioMixerGroup outputMixerGroup;
    }

    [SerializeField] private List<SfxEntry> entries = new();

    public bool TryGet(string tag, out SfxEntry entry)
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

    public AudioClip GetRandomClip(SfxEntry entry, System.Random rng)
    {
        if (entry == null || entry.clips == null || entry.clips.Count == 0) return null;
        int idx = rng.Next(0, entry.clips.Count);
        return entry.clips[idx];
    }
}
