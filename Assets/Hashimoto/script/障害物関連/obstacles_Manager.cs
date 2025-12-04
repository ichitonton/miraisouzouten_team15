using UnityEngine;

public class obstacles_Manager : MonoBehaviour
{
	[SerializeField] private float weight = 0.0f;					// オブジェクトの重さ
	[SerializeField] private int peaces = 0;						// 1つのオブジェクトで何個分？
	[SerializeField] private Transform m_transform;					// オブジェクトの座標
	Vector3 SetTransform;

	private void Start()
	{
		SetTransform = m_transform.position;
	}

	public float GetWeight()
	{
		return weight;
	}

	public int GetPeaces()
	{
		return peaces;
	}

	public void SetReset()
	{
		this.gameObject.transform.position = SetTransform;
	}
}
