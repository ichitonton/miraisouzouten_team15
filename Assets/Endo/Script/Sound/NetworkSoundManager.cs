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

	// ===== 追加：ループSE管理 =====
	// tag -> ループ再生に使ってるAudioSource
	private readonly Dictionary<string, AudioSource> _loopSfxByTag = new();
	// 2Dループ専用（ワンショット2Dと干渉しないよう分ける）
	private AudioSource _loop2DSource;

	private void Awake()
	{
		//シングルトンのインスタンス生成
		if (Instance == null)
		{
			Instance = this;
			//DontDestroyOnLoad(gameObject);
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

		// 追加：2Dループ用AudioSourceを別で用意（実行時生成）
		// 事実：sfx2DSourceをそのままループに使うとUI音などのPlayOneShotと干渉する
		if (_loop2DSource == null)
		{
			var go = new GameObject("Loop2DSource");
			go.transform.SetParent(transform, false);
			_loop2DSource = go.AddComponent<AudioSource>();
			_loop2DSource.playOnAwake = false;
			_loop2DSource.loop = false;
			_loop2DSource.spatialBlend = 0f; // 2D固定
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

	// ===== 追加：ループSE API =====

	/// <summary>
	/// ループSE開始。AllClientsならサーバー経由で全員開始。
	/// spatial=trueなら3Dループ。positionはspatial時のみ有効。
	/// </summary>
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

	/// <summary>
	/// ループSE停止。AllClientsならサーバー経由で全員停止。
	/// </summary>
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

	// ===== 追加：ループSE RPC =====
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
		if (bgmDatabase != null && bgmSource.clip != null)
		{
			fade = 0.2f;
		}
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
		bgmSource.volume = startVol;
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
		if (clip == null) return;

		float pitch = 1f;
		if (entry.randomizePitch)
		{
			float min = Mathf.Min(entry.pitchMin, entry.pitchMax);
			float max = Mathf.Max(entry.pitchMin, entry.pitchMax);
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

	// ===== 追加：ループSEのローカル実装 =====
	private void StartLoopSfxLocal(string tag, bool spatial, Vector3 pos, int seed)
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

		// 既に回ってるなら、位置だけ更新して終わり（3Dの場合）
		if (_loopSfxByTag.TryGetValue(tag, out var playingSrc) && playingSrc != null)
		{
			if (spatial) playingSrc.transform.position = pos;
			return;
		}

		// 同時再生制限（ループは「開始で+1」「停止で-1」方式）
		if (entry.maxSimultaneous > 0)
		{
			_simultaneousCountByTag.TryGetValue(tag, out int count);
			if (count >= entry.maxSimultaneous) return;
			_simultaneousCountByTag[tag] = count + 1;
		}

		var rng = new System.Random(seed);
		var clip = sfxDatabase.GetRandomClip(entry, rng);
		if (clip == null)
		{
			// 失敗したのでカウント戻す
			DecreaseSimultaneousImmediate(tag, entry);
			return;
		}

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
			// 2Dループ専用srcを使う（ワンショットと干渉しない）
			src = _loop2DSource;
			if (src == null)
			{
				Debug.LogWarning("[NetworkSoundManager] loop2DSource missing.");
				DecreaseSimultaneousImmediate(tag, entry);
				return;
			}

			src.spatialBlend = 0f;
		}
		else
		{
			src = GetFree3DSourceForLoop();
			if (src == null)
			{
				DecreaseSimultaneousImmediate(tag, entry);
				return;
			}

			src.transform.position = pos;
			src.spatialBlend = entry.spatialBlend;
			src.minDistance = entry.minDistance;
			src.maxDistance = entry.maxDistance;
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
		if (!_loopSfxByTag.TryGetValue(tag, out var src) || src == null)
			return;

		src.Stop();
		src.clip = null;
		src.loop = false;

		_loopSfxByTag.Remove(tag);

		// 同時再生カウントを戻す（ループは停止で-1）
		if (sfxDatabase != null && sfxDatabase.TryGet(tag, out var entry) && entry != null)
		{
			DecreaseSimultaneousImmediate(tag, entry);
		}
		else
		{
			// entry取れない場合でも、カウントが増えっぱなしになるの防止（最低限）
			if (_simultaneousCountByTag.TryGetValue(tag, out int c))
				_simultaneousCountByTag[tag] = Mathf.Max(0, c - 1);
		}
	}

	private AudioSource GetNext3DSource()
	{
		if (_sfx3DPool.Count == 0) return null;
		var src = _sfx3DPool[_sfx3DPoolIndex];
		_sfx3DPoolIndex = (_sfx3DPoolIndex + 1) % _sfx3DPool.Count;
		return src;
	}

	// 追加：ループ用は「今ループに使われてない3Dソース」を優先で取る
	private AudioSource GetFree3DSourceForLoop()
	{
		if (_sfx3DPool.Count == 0) return null;

		for (int i = 0; i < _sfx3DPool.Count; i++)
		{
			var candidate = _sfx3DPool[i];
			bool isUsedByLoop = false;
			foreach (var kv in _loopSfxByTag)
			{
				if (kv.Value == candidate) { isUsedByLoop = true; break; }
			}
			if (!isUsedByLoop) return candidate;
		}

		// 空きが無いなら諦め（ループが途切れる事故を避けたい）
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
		{
			c = Mathf.Max(0, c - 1);
			_simultaneousCountByTag[tag] = c;
		}
	}

	// 追加：ループ開始失敗/停止で即座にカウント戻す用
	private void DecreaseSimultaneousImmediate(string tag, NetworkSeDatabase.SfxEntry entry)
	{
		if (entry.maxSimultaneous <= 0) return;
		if (_simultaneousCountByTag.TryGetValue(tag, out int c))
			_simultaneousCountByTag[tag] = Mathf.Max(0, c - 1);
	}
}
