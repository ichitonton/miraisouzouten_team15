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
	[SerializeField] private AudioSource bgmSource;
	[SerializeField] private AudioSource sfx2DSource;
	[SerializeField] private AudioSource sfx3DSourcePrefab;

	[Header("SFX Pool")]
	[SerializeField] private int sfx3DPoolSize = 16;

	private readonly List<AudioSource> _sfx3DPool = new();
	private int _sfx3DPoolIndex = 0;

	private readonly Dictionary<string, float> _lastSfxTimeByTag = new();
	private readonly Dictionary<string, int> _simultaneousCountByTag = new();

	private Coroutine _bgmFadeCoroutine;

	public static NetworkSoundManager Instance;

	// ===== ループSE管理 =====
	private readonly Dictionary<string, AudioSource> _loopSfxByTag = new();
	private AudioSource _loop2DSource;

	// ★追加：3Dループに使ってるAudioSourceを記録して、単発が奪わないようにする
	private readonly HashSet<AudioSource> _loop3DSources = new();

	private void Awake()
	{
		if (Instance == null) Instance = this;
		else { Destroy(gameObject); return; }

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

		if (_loop2DSource == null)
		{
			var go = new GameObject("Loop2DSource");
			go.transform.SetParent(transform, false);
			_loop2DSource = go.AddComponent<AudioSource>();
			_loop2DSource.playOnAwake = false;
			_loop2DSource.loop = false;
			_loop2DSource.spatialBlend = 0f;
		}
	}

	// =========================
	// Public API
	// =========================
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

	public void StartLoopSfx(string tag, SoundScope scope, bool spatial, Vector3 position = default)
	{
		if (scope == SoundScope.LocalOnly)
		{
			int seed = Environment.TickCount;
			StartLoopSfxLocal(tag, spatial, position, seed);
			return;
		}

		if (IsServer)
		{
			int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
			StartLoopSfxClientRpc(tag, spatial, position, seed);
		}
		else
		{
			RequestStartLoopSfxServerRpc(tag, spatial, position);
		}
	}

	public void StopLoopSfx(string tag, SoundScope scope)
	{
		if (scope == SoundScope.LocalOnly)
		{
			StopLoopSfxLocal(tag);
			return;
		}

		if (IsServer)
		{
			StopLoopSfxClientRpc(tag);
		}
		else
		{
			RequestStopLoopSfxServerRpc(tag);
		}
	}

	// =========================
	// RPCs
	// =========================
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

	[ServerRpc(RequireOwnership = false)]
	private void RequestStartLoopSfxServerRpc(string tag, bool spatial, Vector3 pos)
	{
		int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
		StartLoopSfxClientRpc(tag, spatial, pos, seed);
	}

	[ClientRpc]
	private void StartLoopSfxClientRpc(string tag, bool spatial, Vector3 pos, int seed)
	{
		StartLoopSfxLocal(tag, spatial, pos, seed);
	}

	[ServerRpc(RequireOwnership = false)]
	private void RequestStopLoopSfxServerRpc(string tag)
	{
		StopLoopSfxClientRpc(tag);
	}

	[ClientRpc]
	private void StopLoopSfxClientRpc(string tag)
	{
		StopLoopSfxLocal(tag);
	}

	// =========================
	// Local Implementations
	// =========================

	private void PlaySfxLocal(string tag, bool spatial, Vector3 pos, int seed)
	{
		if (sfxDatabase == null) return;
		if (!sfxDatabase.TryGet(tag, out var entry) || entry == null) return;

		float now = Time.unscaledTime;
		if (entry.cooldownSeconds > 0f)
		{
			if (_lastSfxTimeByTag.TryGetValue(tag, out float last) && (now - last) < entry.cooldownSeconds)
				return;
			_lastSfxTimeByTag[tag] = now;
		}

		if (entry.maxSimultaneous > 0)
		{
			_simultaneousCountByTag.TryGetValue(tag, out int count);
			if (count >= entry.maxSimultaneous) return;
			_simultaneousCountByTag[tag] = count + 1;
		}

		var rng = new System.Random(seed);
		var clip = sfxDatabase.GetRandomClip(entry, rng);
		if (clip == null) { DecreaseSimultaneousLater(tag, entry, 0.1f); return; }

		float pitch = 1f;
		if (entry.randomizePitch)
		{
			float min = Mathf.Min(entry.pitchMin, entry.pitchMax);
			float max = Mathf.Max(entry.pitchMin, entry.pitchMax);
			pitch = Mathf.Lerp(min, max, (float)rng.NextDouble());
		}

		if (!spatial)
		{
			if (sfx2DSource == null) { DecreaseSimultaneousLater(tag, entry, clip.length); return; }
			if (entry.outputMixerGroup != null) sfx2DSource.outputAudioMixerGroup = entry.outputMixerGroup;
			sfx2DSource.pitch = pitch;
			sfx2DSource.PlayOneShot(clip, entry.volume);
			DecreaseSimultaneousLater(tag, entry, clip.length);
			return;
		}

		// ★ここが重要：ループに使ってる3Dソースを避ける
		var src = GetNext3DSourceAvoidLoop();
		if (src == null) { DecreaseSimultaneousLater(tag, entry, clip.length); return; }

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

	private void StartLoopSfxLocal(string tag, bool spatial, Vector3 pos, int seed)
	{
		if (sfxDatabase == null) return;
		if (!sfxDatabase.TryGet(tag, out var entry) || entry == null) return;

		if (_loopSfxByTag.TryGetValue(tag, out var playingSrc) && playingSrc != null)
		{
			if (spatial) playingSrc.transform.position = pos;
			return;
		}

		if (entry.maxSimultaneous > 0)
		{
			_simultaneousCountByTag.TryGetValue(tag, out int count);
			if (count >= entry.maxSimultaneous) return;
			_simultaneousCountByTag[tag] = count + 1;
		}

		var rng = new System.Random(seed);
		var clip = sfxDatabase.GetRandomClip(entry, rng);
		if (clip == null) { DecreaseSimultaneousImmediate(tag, entry); return; }

		float pitch = 1f;
		if (entry.randomizePitch)
		{
			float min = Mathf.Min(entry.pitchMin, entry.pitchMax);
			float max = Mathf.Max(entry.pitchMin, entry.pitchMax);
			pitch = Mathf.Lerp(min, max, (float)rng.NextDouble());
		}

		AudioSource src;
		if (!spatial)
		{
			src = _loop2DSource;
			if (src == null) { DecreaseSimultaneousImmediate(tag, entry); return; }
			src.spatialBlend = 0f;
		}
		else
		{
			src = GetFree3DSourceForLoop();
			if (src == null) { DecreaseSimultaneousImmediate(tag, entry); return; }

			src.transform.position = pos;
			src.spatialBlend = entry.spatialBlend;
			src.minDistance = entry.minDistance;
			src.maxDistance = entry.maxDistance;

			// ★ループに使ってる3Dソースとして記録
			_loop3DSources.Add(src);
		}

		if (entry.outputMixerGroup != null) src.outputAudioMixerGroup = entry.outputMixerGroup;

		src.Stop();
		src.clip = clip;
		src.pitch = pitch;
		src.volume = entry.volume;
		src.loop = true;
		src.Play();

		_loopSfxByTag[tag] = src;
	}

	private void StopLoopSfxLocal(string tag)
	{
		if (!_loopSfxByTag.TryGetValue(tag, out var src) || src == null) return;

		// ★止める前に「ループ3D使用中」から外す（3Dだった場合だけ）
		_loop3DSources.Remove(src);

		src.Stop();
		src.clip = null;
		src.loop = false;

		_loopSfxByTag.Remove(tag);

		if (sfxDatabase != null && sfxDatabase.TryGet(tag, out var entry) && entry != null)
		{
			DecreaseSimultaneousImmediate(tag, entry);
		}
		else
		{
			if (_simultaneousCountByTag.TryGetValue(tag, out int c))
				_simultaneousCountByTag[tag] = Mathf.Max(0, c - 1);
		}
	}

	// ★修正版：ループに使われてるAudioSourceを避けて取る
	private AudioSource GetNext3DSourceAvoidLoop()
	{
		if (_sfx3DPool.Count == 0) return null;

		for (int i = 0; i < _sfx3DPool.Count; i++)
		{
			var src = _sfx3DPool[_sfx3DPoolIndex];
			_sfx3DPoolIndex = (_sfx3DPoolIndex + 1) % _sfx3DPool.Count;

			if (_loop3DSources.Contains(src))
				continue; // ループ用は触らない

			return src;
		}

		// 全部ループに占領されてたら諦め
		return null;
	}

	private AudioSource GetFree3DSourceForLoop()
	{
		if (_sfx3DPool.Count == 0) return null;

		for (int i = 0; i < _sfx3DPool.Count; i++)
		{
			var candidate = _sfx3DPool[i];
			if (_loop3DSources.Contains(candidate)) continue;
			return candidate;
		}
		return null;
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
			_simultaneousCountByTag[tag] = Mathf.Max(0, c - 1);
	}

	private void DecreaseSimultaneousImmediate(string tag, NetworkSeDatabase.SfxEntry entry)
	{
		if (entry.maxSimultaneous <= 0) return;
		if (_simultaneousCountByTag.TryGetValue(tag, out int c))
			_simultaneousCountByTag[tag] = Mathf.Max(0, c - 1);
	}

	// =========================
	// BGM Public API
	// =========================

	public void PlayBgm(string tag, SoundScope scope, bool? loopOverride = null)
	{
		if (scope == SoundScope.LocalOnly)
		{
			PlayBgmLocal(tag, loopOverride, seed: Environment.TickCount);
			return;
		}

		if (IsServer)
		{
			int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
			PlayBgmClientRpc(tag, loopOverride.HasValue, loopOverride.GetValueOrDefault(), seed);
		}
		else
		{
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

	// =========================
	// BGM RPCs
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

	// =========================
	// BGM Local Implementations
	// =========================

	private void PlayBgmLocal(string tag, bool? loopOverride, int seed)
	{
		if (bgmSource == null || bgmDatabase == null)
		{
			Debug.LogWarning("[NetworkSoundManager] BGM Source or Database missing.");
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

		if (entry.outputMixerGroup != null) bgmSource.outputAudioMixerGroup = entry.outputMixerGroup;

		bool loop = loopOverride ?? entry.loop;
		float targetVol = entry.volume;
		float fade = entry.fadeSeconds;

		if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
		_bgmFadeCoroutine = StartCoroutine(FadeSwapBgm(clip, loop, targetVol, fade));
	}

	private void StopBgmLocal(float fadeSecondsOverride)
	{
		if (bgmSource == null) return;

		float fade = 0f;
		if (bgmDatabase != null && bgmSource.clip != null) fade = 0.2f;
		if (fadeSecondsOverride >= 0f) fade = fadeSecondsOverride;

		if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
		_bgmFadeCoroutine = StartCoroutine(FadeOutBgm(fade));
	}

	private IEnumerator FadeSwapBgm(AudioClip newClip, bool loop, float targetVol, float fadeSeconds)
	{
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
		if (!bgmSource.isPlaying) yield break;

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
		bgmSource.volume = startVol;
	}


}
