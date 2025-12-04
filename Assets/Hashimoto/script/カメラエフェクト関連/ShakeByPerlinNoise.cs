using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShakeByPerlinNoise : MonoBehaviour
{
	public static ShakeByPerlinNoise Instance { get; private set; }

	private void Awake()
	{
		Instance = this; // シーン上に1個だけ想定
	}
	[Header("Shake Settings")]
	[SerializeField] private float _duration = 0.3f;   // 揺れる時間
	[SerializeField] private float _strength = 5.0f;   // パーリンノイズの揺れ幅
	[SerializeField] private float _vibrato = 10.0f;   // 最大振れ幅

	private struct ShakeInfo
	{
		public float Duration;
		public float Strength;
		public float Vibrato;
		public Vector2 RandomOffset;

		public ShakeInfo(float duration, float strength, float vibrato, Vector2 randomOffset)
		{
			Duration = duration;
			Strength = strength;
			Vibrato = vibrato;
			RandomOffset = randomOffset;
		}
	}

	private ShakeInfo _shakeInfo;
	private Vector3 _initPosition;
	private bool _isDoShake;
	private float _totalShakeTime;

	private void LateUpdate()
	{
		if (!_isDoShake) return;

		transform.localPosition = GetUpdateShakePosition(_shakeInfo, _totalShakeTime, _initPosition);
		Debug.LogWarning("Shake pos : " + transform.localPosition);

		_totalShakeTime += Time.deltaTime;
		if (_totalShakeTime >= _shakeInfo.Duration)
		{
			_isDoShake = false;
			transform.localPosition = _initPosition;
		}
	}

	private Vector3 GetUpdateShakePosition(ShakeInfo shakeInfo, float totalTime, Vector3 initPos)
	{
		float randomX = (Mathf.PerlinNoise(shakeInfo.RandomOffset.x + shakeInfo.Strength * totalTime, 0f) - 0.5f) * 2f;
		float randomY = (Mathf.PerlinNoise(shakeInfo.RandomOffset.y + shakeInfo.Strength * totalTime, 0f) - 0.5f) * 2f;

		randomX *= shakeInfo.Strength;
		randomY *= shakeInfo.Strength;

		float ratio = 1f - (totalTime / shakeInfo.Duration);
		randomX = Mathf.Clamp(randomX, -shakeInfo.Vibrato * ratio, shakeInfo.Vibrato * ratio);
		randomY = Mathf.Clamp(randomY, -shakeInfo.Vibrato * ratio, shakeInfo.Vibrato * ratio);

		// カメラの向きに合わせた揺れ
		Vector3 offset = transform.rotation * new Vector3(randomX, randomY, 0f);

		return initPos + offset;
	}

	// 外部から呼び出す用（Player側・爆風側）
	public void StartShake()
	{
		Debug.LogWarning("ShakeByPerlinNoise.StartShake 呼ばれた");
		_initPosition = transform.localPosition;
		_shakeInfo = new ShakeInfo(
			_duration,
			_strength,
			_vibrato,
			new Vector2(Random.Range(0, 100f), Random.Range(0, 100f))
		);

		_isDoShake = true;
		_totalShakeTime = 0f;
	}

	// 手動で数値入れたい人用オーバーロード
	public void StartShake(float duration, float strength, float vibrato)
	{
		_duration = duration;
		_strength = strength;
		_vibrato = vibrato;
		StartShake();
	}
}
