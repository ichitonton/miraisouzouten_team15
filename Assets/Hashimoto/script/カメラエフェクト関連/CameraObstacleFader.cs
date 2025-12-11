using UnityEngine;
using System.Collections.Generic;

public class CameraObstacleFader : MonoBehaviour
{
	// シェーダー側のプロパティ名と一致させる
	static readonly int FadeId = Shader.PropertyToID("_FadeMultiplier");

	[SerializeField, Range(0f, 1f)]
	float _fadeOutValue = 0.3f; // この値までフェードさせる

	[SerializeField]
	float _fadeSpeed = 4f;      // フェード速度

	float _current = 1f;        // 現在のフェード値
	float _target = 1f;         // 目標フェード値

	Renderer[] _renderers;
	MaterialPropertyBlock _mpb;

	void Awake()
	{
		// 子を含めて全部拾う（非アクティブも含めたいなら true）
		_renderers = GetComponentsInChildren<Renderer>(true);
		_mpb = new MaterialPropertyBlock();

		// 初期状態を 1 にしておく
		ApplyFadeInstant(1f);
	}

	// true でフェード、false で元に戻す
	public void SetFaded(bool faded)
	{
		_target = faded ? _fadeOutValue : 1f;
	}

	void Update()
	{
		if (Mathf.Approximately(_current, _target))
			return;

		_current = Mathf.MoveTowards(_current, _target, _fadeSpeed * Time.deltaTime);
		ApplyFadeInstant(_current);
	}

	// 全 Renderer に対してフェード値を書き込む
	void ApplyFadeInstant(float value)
	{
		foreach (var r in _renderers)
		{
			if (r == null) continue;

			r.GetPropertyBlock(_mpb);
			_mpb.SetFloat(FadeId, value);
			r.SetPropertyBlock(_mpb);
		}
	}
}
