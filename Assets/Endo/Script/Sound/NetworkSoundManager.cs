using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSoundManager : NetworkBehaviour
{
    public enum SoundScope
    {
        LocalOnly,
        AllClients
    }
    [Header("Databases")]
    [SerializeField] private NetworkBgmDatabase bgmDatabase;
    [SerializeField] private NetworkSeDatabase sfxDatabase;

    [Header("AudioSources")]
    [Tooltip("BGM用（基本2D）")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("2D SE用（UI音など）")]
    [SerializeField] private AudioSource sfx2DSource;

    [Tooltip("3D SE用。位置に生成して使う（プール）")]
    [SerializeField] private AudioSource sfx3DSourcePrefab;

    [Header("SFX Pool")]
    [SerializeField] private int sfx3DPoolSize = 16;

    // --- runtime ---
    private readonly List<AudioSource> _sfx3DPool = new();
    private int _sfx3DPoolIndex = 0;

    // anti-spam
    private readonly Dictionary<string, float> _lastSfxTimeByTag = new();
    private readonly Dictionary<string, int> _simultaneousCountByTag = new();

    private Coroutine _bgmFadeCoroutine;

    public static NetworkSoundManager Instance;

    private void Awake()
    {
        //シングルトンのインスタンス生成
        if (Instance == null)
        {
            Instance = this;
            //シーンの切り替えで消えない
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        if (sfx3DSourcePrefab != null)
        {
            for (int i = 0; i < Mathf.Max(1, sfx3DPoolSize); i++)
            {
                var src = Instantiate(sfx3DSourcePrefab, Vector3.zero, Quaternion.identity, transform);
                src.playOnAwake = false;
                src.loop = false;
                _sfx3DPool.Add(src);
            }
        }
    }

    // =========================
    // Public API
    // =========================

    /// <summary>
    /// BGM再生。scope=AllClients の場合、Server経由で全員に再生指示。
    /// loopOverride=nullならDBの設定を使用。
    /// </summary>
    public void PlayBgm(string tag, SoundScope scope, bool? loopOverride = null)
    {
        if (scope == SoundScope.LocalOnly)
        {
            PlayBgmLocal(tag, loopOverride, seed: Environment.TickCount);
            return;
        }

        // AllClients
        if (IsServer)
        {
            // 全員で同じ曲を選ぶためseed固定して投げる（サーバーが決める）
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            PlayBgmClientRpc(tag, loopOverride.HasValue, loopOverride.GetValueOrDefault(), seed);
        }
        else
        {
            // クライアントから全員再生したいなら、サーバーに依頼
            RequestPlayBgmServerRpc(tag, loopOverride.HasValue, loopOverride.GetValueOrDefault());
        }
    }

    public void StopBgm(SoundScope scope, float fadeSecondsOverride = -1f)
    {
        if (scope == SoundScope.LocalOnly)
        {
            StopBgmLocal(fadeSecondsOverride);
            return;
        }

        if (IsServer)
        {
            StopBgmClientRpc(fadeSecondsOverride);
        }
        else
        {
            RequestStopBgmServerRpc(fadeSecondsOverride);
        }
    }

    /// <summary>
    /// SE再生。spatial=trueなら3D。positionは spatial のときのみ有効。
    /// </summary>
    public void PlaySfx(string tag, SoundScope scope, bool spatial, Vector3 position = default)
    {
        if (scope == SoundScope.LocalOnly)
        {
            int seed = Environment.TickCount;
            PlaySfxLocal(tag, spatial, position, seed);
            return;
        }

        if (IsServer)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            PlaySfxClientRpc(tag, spatial, position, seed);
        }
        else
        {
            RequestPlaySfxServerRpc(tag, spatial, position);
        }
    }

    // =========================
    // RPCs
    // =========================

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayBgmServerRpc(string tag, bool hasLoopOverride, bool loopOverride)
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        PlayBgmClientRpc(tag, hasLoopOverride, loopOverride, seed);
    }

    [ClientRpc]
    private void PlayBgmClientRpc(string tag, bool hasLoopOverride, bool loopOverride, int seed)
    {
        bool? loop = hasLoopOverride ? loopOverride : (bool?)null;
        PlayBgmLocal(tag, loop, seed);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStopBgmServerRpc(float fadeSecondsOverride)
    {
        StopBgmClientRpc(fadeSecondsOverride);
    }

    [ClientRpc]
    private void StopBgmClientRpc(float fadeSecondsOverride)
    {
        StopBgmLocal(fadeSecondsOverride);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaySfxServerRpc(string tag, bool spatial, Vector3 pos)
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        PlaySfxClientRpc(tag, spatial, pos, seed);
    }

    [ClientRpc]
    private void PlaySfxClientRpc(string tag, bool spatial, Vector3 pos, int seed)
    {
        PlaySfxLocal(tag, spatial, pos, seed);
    }

    // =========================
    // Local Implementations
    // =========================

    private void PlayBgmLocal(string tag, bool? loopOverride, int seed)
    {
        if (bgmSource == null || bgmDatabase == null)
        {
            Debug.LogWarning($"[NetworkSoundManager] BGM Source or Database missing.");
            return;
        }

        if (!bgmDatabase.TryGet(tag, out var entry) || entry == null)
        {
            Debug.LogWarning($"[NetworkSoundManager] BGM tag not found: {tag}");
            return;
        }

        var rng = new System.Random(seed);
        var clip = bgmDatabase.GetRandomClip(entry, rng);
        if (clip == null)
        {
            Debug.LogWarning($"[NetworkSoundManager] BGM clips empty: {tag}");
            return;
        }

        // Mixer
        if (entry.outputMixerGroup != null) bgmSource.outputAudioMixerGroup = entry.outputMixerGroup;

        bool loop = loopOverride ?? entry.loop;
        float targetVol = entry.volume;
        float fade = entry.fadeSeconds;

        // フェード切り替え
        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = StartCoroutine(FadeSwapBgm(clip, loop, targetVol, fade));
    }

    private void StopBgmLocal(float fadeSecondsOverride)
    {
        if (bgmSource == null) return;

        float fade = 0f;
        if (bgmDatabase != null && bgmSource.clip != null)
        {
            // clipからtag逆引きはしない（シンプルにoverride優先）
            fade = 0.2f;
        }
        if (fadeSecondsOverride >= 0f) fade = fadeSecondsOverride;

        if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = StartCoroutine(FadeOutBgm(fade));
    }

    private IEnumerator FadeSwapBgm(AudioClip newClip, bool loop, float targetVol, float fadeSeconds)
    {
        // Fade out current
        if (bgmSource.isPlaying && fadeSeconds > 0f)
        {
            float startVol = bgmSource.volume;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / fadeSeconds);
                yield return null;
            }
        }

        bgmSource.Stop();
        bgmSource.clip = newClip;
        bgmSource.loop = loop;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // Fade in
        if (fadeSeconds > 0f)
        {
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(0f, targetVol, t / fadeSeconds);
                yield return null;
            }
        }
        bgmSource.volume = targetVol;
    }

    private IEnumerator FadeOutBgm(float fadeSeconds)
    {
        if (!bgmSource.isPlaying)
            yield break;

        if (fadeSeconds <= 0f)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            yield break;
        }

        float startVol = bgmSource.volume;
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / fadeSeconds);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
        bgmSource.volume = startVol; // 次回のために戻す
    }

    private void PlaySfxLocal(string tag, bool spatial, Vector3 pos, int seed)
    {
        if (sfxDatabase == null)
        {
            Debug.LogWarning("[NetworkSoundManager] SFX Database missing.");
            return;
        }

        if (!sfxDatabase.TryGet(tag, out var entry) || entry == null)
        {
            Debug.LogWarning($"[NetworkSoundManager] SFX tag not found: {tag}");
            return;
        }

        // クールダウン（タグ別）
        float now = Time.unscaledTime;
        if (entry.cooldownSeconds > 0f)
        {
            if (_lastSfxTimeByTag.TryGetValue(tag, out float last) && (now - last) < entry.cooldownSeconds)
                return;

            _lastSfxTimeByTag[tag] = now;
        }

        // 同時再生制限（簡易）
        if (entry.maxSimultaneous > 0)
        {
            _simultaneousCountByTag.TryGetValue(tag, out int count);
            if (count >= entry.maxSimultaneous) return;
            _simultaneousCountByTag[tag] = count + 1;
        }

        var rng = new System.Random(seed);
        var clip = sfxDatabase.GetRandomClip(entry, rng);
        if (clip == null) return;

        float pitch = 1f;
        if (entry.randomizePitch)
        {
            float min = Mathf.Min(entry.pitchMin, entry.pitchMax);
            float max = Mathf.Max(entry.pitchMin, entry.pitchMax);
            // rngで安定させる
            pitch = Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        if (!spatial)
        {
            if (sfx2DSource == null)
            {
                Debug.LogWarning("[NetworkSoundManager] sfx2DSource missing.");
                DecreaseSimultaneousLater(tag, entry, clip.length);
                return;
            }

            if (entry.outputMixerGroup != null) sfx2DSource.outputAudioMixerGroup = entry.outputMixerGroup;
            sfx2DSource.pitch = pitch;
            sfx2DSource.PlayOneShot(clip, entry.volume);

            DecreaseSimultaneousLater(tag, entry, clip.length);
        }
        else
        {
            var src = GetNext3DSource();
            if (src == null)
            {
                DecreaseSimultaneousLater(tag, entry, clip.length);
                return;
            }

            src.transform.position = pos;
            src.spatialBlend = entry.spatialBlend;
            src.minDistance = entry.minDistance;
            src.maxDistance = entry.maxDistance;
            src.pitch = pitch;
            src.loop = false;
            if (entry.outputMixerGroup != null) src.outputAudioMixerGroup = entry.outputMixerGroup;

            src.PlayOneShot(clip, entry.volume);

            DecreaseSimultaneousLater(tag, entry, clip.length);
        }
    }

    private AudioSource GetNext3DSource()
    {
        if (_sfx3DPool.Count == 0) return null;
        var src = _sfx3DPool[_sfx3DPoolIndex];
        _sfx3DPoolIndex = (_sfx3DPoolIndex + 1) % _sfx3DPool.Count;
        return src;
    }

    private void DecreaseSimultaneousLater(string tag, NetworkSeDatabase.SfxEntry entry, float seconds)
    {
        if (entry.maxSimultaneous <= 0) return;
        StartCoroutine(DecreaseLater(tag, Mathf.Max(0.01f, seconds)));
    }

    private IEnumerator DecreaseLater(string tag, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (_simultaneousCountByTag.TryGetValue(tag, out int c))
        {
            c = Mathf.Max(0, c - 1);
            _simultaneousCountByTag[tag] = c;
        }
    }
}
