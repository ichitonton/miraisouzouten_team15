using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ItemBox : NetworkBehaviour
{
	[Header("箱の見た目（子オブジェクト）")]
	[SerializeField] GameObject _boxObject;

	[Header("割れるエフェクト（PooledNetworkObject）")]
	[SerializeField] GameObject eff_Item_Debris;

	[Header("復活までの時間")]
	[SerializeField] float respawnTime = 5f;

	[Header("アイテムが抽選される時間")]
	[SerializeField] float itemChoose = 1.0f;

	[Header("復活アニメの長さ")]
	[SerializeField] float appearDuration = 0.5f;

	[Header("復活時の回転速度（度/秒）")]
	[SerializeField] float respawnRotateSpeed = -180f;

	bool _used = false;
	Collider[] _colliders;
	Vector3 _originalScale;

	private void Awake()
	{
		_colliders = GetComponents<Collider>();
	}

	private void Start()
	{
		if (_boxObject != null)
			_originalScale = _boxObject.transform.localScale;
	}



	private void OnTriggerEnter(Collider other)
	{
		if (!IsServer) return;
		if (!other.gameObject.CompareTag("Player")) return;
		if (_used) return;
		var player = other.GetComponentInParent<MovePlayerKey>();
		if (player == null) return;

		player.LotteryHaveItem(itemChoose);

		_used = true;

		// 箱を非表示にする
		if (_boxObject != null)
		{
			//_boxObject.GetComponent<NetworkObject>().Despawn(false);//falseすればSetSctive(falseとほぼ同じ)
			//_boxObject.SetActive(false);
			SetEffectClientRpc(false);
		}

		// 判定オフ
		//SetColliders(false);

		// 割れるエフェクト
		if (eff_Item_Debris != null)
		{
			NetworkEffectSpawner.Instance.PlayEffect("item_Debris", _boxObject.transform.position, _boxObject.transform.rotation);
			//NetworkEffectSpawner.Instance.
			//Instantiate(eff_Item_Debris,
			//	_boxObject != null ? _boxObject.transform.position : transform.position,
			//	transform.rotation);
		}

		// 復活まで
		StartCoroutine(RespawnRoutine());
	}

	IEnumerator RespawnRoutine()
	{
		yield return new WaitForSeconds(respawnTime);

		_used = false;

		// 箱をいったん Scale 0 に
		SetEffectClientRpc(true);
		_boxObject.transform.localScale = Vector3.zero;

		// Collider 戻す
		//SetColliders(true);

		// 徐々に元のサイズに戻しながら回転
		float t = 0f;
		while (t < 1f)
		{
			t += Time.deltaTime / appearDuration;

			// 拡大
			_boxObject.transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, t);

			// 回転（Y軸メイン）
			_boxObject.transform.Rotate(0, respawnRotateSpeed * Time.deltaTime, 0);

			yield return null;
		}

		_boxObject.transform.localScale = _originalScale;
	}

	void SetColliders(bool enabled)
	{
		foreach (var col in _colliders)
			col.enabled = enabled;
	}

	[ClientRpc]
	private void SetEffectClientRpc(bool enabled)
	{
		_boxObject.SetActive(enabled);
		SetColliders(enabled);
	}
}
