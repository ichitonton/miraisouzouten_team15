using UnityEngine;
using System.Collections.Generic;

public class CameraObstacleFader : MonoBehaviour
{
	// シェーダーのプロパティ名と一致させる
	static readonly int FadeId = Shader.PropertyToID("_FadeMultiplier");

	[SerializeField] float _fadeOutValue = 0.3f; // これくらいまで暗くする
	[SerializeField] float _fadeSpeed = 4f;      // どれくらいの速さで変化するか

	float _current = 1f;
	float _target = 1f;

	Renderer[] _renderers;
	MaterialPropertyBlock _mpb;

	void Awake()
	{
		_renderers = GetComponentsInChildren<Renderer>();
		_mpb = new MaterialPropertyBlock();
	}

	public void SetFaded(bool faded)
	{
		// true なら暗く、false なら元に戻す
		_target = faded ? _fadeOutValue : 1f;
	}

	void Update()
	{
		if (Mathf.Approximately(_current, _target)) return;

		_current = Mathf.MoveTowards(_current, _target, _fadeSpeed * Time.deltaTime);

		foreach (var r in _renderers)
		{
			if (r == null) continue;
			r.GetPropertyBlock(_mpb);
			_mpb.SetFloat(FadeId, _current);
			r.SetPropertyBlock(_mpb);
		}
	}
}
