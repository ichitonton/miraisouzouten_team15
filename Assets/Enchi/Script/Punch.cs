using Unity.Netcode;
using UnityEngine;

public class Punch : NetworkBehaviour
{
    [SerializeField] private float _punchForce = 10.0f;
	[SerializeField] private float _stunTime = 1.0f;
	int _punchDamage = 10;

	MovePlayerKey _owner;

	private int _hitEffectId = 4; // Hitエフェクト
	private int _hitDmgEffectId = 7; // HitDmgエフェクト

	void Start()
	{
		_owner = GetComponentInParent<MovePlayerKey>();

		if (_owner == null)
		{
			Debug.LogWarning($"[Punch WARNING] _owner が null。Punch がプレイヤーの子についていない可能性あり。（{gameObject.name}）");
		}
		else
		{
			Debug.LogWarning($"[Punch INFO] _owner 設定完了 → {_owner.name}");
		}

		// パラメータ同期
		if (_owner != null)
		{
			_punchForce = _owner.GetPunchForce();
			_stunTime = _owner.GetStunTime();
			_punchDamage = _owner.GetPunchDamage();
		}
	}

    void OnTriggerEnter(Collider other)
	{
		MovePlayerKey otherPlayer = other.GetComponentInParent<MovePlayerKey>();

		// ログ
		Debug.LogWarning($"[Punch HIT] other = {other.name}, owner = {_owner?.name}, otherPlayer = {otherPlayer?.name}");

		// 自分自身なら無視
		if (otherPlayer == _owner)
		{
			Debug.LogWarning($"[Punch IGNORE] 自分自身ヒット → 無視 ({other.name})");
			return;
		}
		if (other.GetComponent<JointLiner>() != null)
		{
			Debug.LogWarning($"[Punch IGNORE] Joint 除外 → {other.name}");
			return;
		}

		//エネミーのセンサー内なら無視
		if (other.GetComponent<HitTrigger>() != null)
		{
			other.GetComponent<HitTrigger>().HitPunch();
        }
        // Rigidbody取得
		Rigidbody rb = other.GetComponentInParent<Rigidbody>();
        if (rb == null)
		{
			Debug.LogWarning($"[Punch IGNORE] Rigidbodyなし → 無視 ({other.name})");
			return;
		}

		// ★プレイヤー判定
		if (otherPlayer != null)
		{
			//スター状態なら無効
			if (otherPlayer.GetUseStar()) return;

            NetworkEffectSpawner.Instance.PlayEffect(_hitDmgEffectId, transform.position, Quaternion.identity);
            NetworkEffectSpawner.Instance.PlayEffect(_hitEffectId, transform.position, Quaternion.identity);

			Debug.Log("otherHitCount" + otherPlayer._hitCount);

			if (otherPlayer._hitCount == 0)
			{
				otherPlayer._hitCount++;
				
			}
			else if (otherPlayer._hitCount == 1)
			{
				otherPlayer._hitCount++;
				otherPlayer.Stun(2f);
				otherPlayer._stun = true;
				
			}
			else if (otherPlayer._stun == true)
			{
				Debug.LogWarning($"[Punch EFFECT] Player HIT → {otherPlayer.name}");
				KnockBack(rb);

				//otherPlayer._hitCount = 0;

				
			}

			//Debug.LogWarning($"[Punch EFFECT] Player HIT → {otherPlayer.name}");
			//if (otherPlayer.ToGetPunch(_punchDamage, _stunTime))
			//	KnockBack(rb);

			//NetworkEffectSpawner.Instance._otherRoot = transform;

		}

		// ★Sweet判定（階層検索）
		if (IsSweets(other))
		{
			Debug.LogWarning($"[Punch EFFECT] Sweet HIT → {other.name}");
			NetworkEffectSpawner.Instance.PlayEffect(_hitEffectId, transform.position, Quaternion.identity);
		}

		//if (other.isTrigger == true) return;
			

		

		var ice = other.GetComponent<IcePillar>();
		if( ice != null )
		{
            Debug.LogWarning($"[Punch EFFECT] Sweet HIT → {other.name}");
            NetworkEffectSpawner.Instance.PlayEffect(_hitEffectId, transform.position, Quaternion.identity);
        }

		//otherPlayer.Stun(2.0f);

		//// ノックバック
		//Debug.Log("ノックバック" + other);
		//KnockBack(rb);
		NetworkSoundManager.Instance.PlaySfx("Punch", NetworkSoundManager.SoundScope.LocalOnly,false);
	}

	void KnockBack(Rigidbody rb)
	{
        rb.AddForce((transform.forward + Vector3.up) * _punchForce, ForceMode.Impulse);
    }
	bool IsSweets(Collider other)
	{
		Transform t = other.transform;
		while (t != null)
		{
			if (t.CompareTag("Sweets"))
				return true;

			t = t.parent;
		}
		return false;
	}
}
