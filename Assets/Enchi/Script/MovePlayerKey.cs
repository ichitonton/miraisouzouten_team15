using NUnit.Framework.Constraints;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class MovePlayerKey : NetworkBehaviour
{
    [SerializeField] KeyCode _up;
    [SerializeField] KeyCode _down;
    [SerializeField] KeyCode _left;
    [SerializeField] KeyCode _right;
    [SerializeField] KeyCode _jump;
    [SerializeField] KeyCode _punchKey;
    [SerializeField] KeyCode _useItemKey;

    [SerializeField] float _moveSpeed = 7.0f;
    [SerializeField] float _jumpForce = 7.0f;

    [SerializeField] float _punchDuration = 0.5f;
    [SerializeField] float _punchDelay = 0.5f;
    [SerializeField] GameObject _punchObj;
    [SerializeField] int _MaxHp = 100;
    [SerializeField] int _punchDamage = 20;
    [SerializeField] float _punchForce = 10.0f;
    [SerializeField] float _stunTime = 1.0f;//パンチした時のスタン時間
    [SerializeField] Transform _haveTrans;//持ってるアイテム
    Vector3 _target;
    [SerializeField] private GameObject _itemBomb;
    [SerializeField] private GameObject _itemBlackHole;
    [SerializeField] private GameObject _itemShouse;
    [SerializeField] private float _itemShoeseDelay = 7.0f;
    [SerializeField] private float _itemShoeseChangeSpeed = 1.5f;
    private bool _itemShoeseUse = false;

    [SerializeField] private GameObject _itemStar;
    [SerializeField] private float _itemStarDelay = 7.0f;
    [SerializeField] private float _itemStarChangeSpeed = 1.3f;
    private bool _itemStarUse = false;
    [SerializeField] float _itemFlightTime = 2.0f; // 投げるオブジェクトがターゲットに到達するまでの時間

    [SerializeField]ParticleSystem _dashParticleSystem;
    [SerializeField]ParticleSystem _mutekiParticleSystem;
    [SerializeField]ParticleSystem _shoeseParticleSystem;
    [SerializeField]ParticleSystem _dyingParticleSystem;

    //エフェクト関連
    [SerializeField] Transform _headPoint; //頭の位置
    private int _punchStartEffectId = 3;   //頭のエフェクト
	private int _hitDyingEffectId = 10; // スタンエフェクト

	//そのPCの中でのプレイヤー番号
	[SerializeField] PlayerNumber _playerNumber = PlayerNumber.None;

    GameObject _effDash2Instance;
    ParticleSystem _effDash2Ps;

    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;
    private bool _canNotInputKey = false;
    private int _currentHp;
    private float _moveSpeedInitial;
    private bool _canPunch = true;
    private ItemType _haveItem = ItemType.None;
    NetworkObjectPool _ObjectPool = null;

    GameObject _item;

    float _animBlend = 0.0f;

    Gamepad gamepad;

    Vector3 _lookVector = Vector3.zero;//向いている方向

    [SerializeField] CanJump _FootCollider;

    Animator _anim;

    //入力受付フラグ
    Vector2 _InputMove = Vector2.zero;
    bool _InputUseItem = false;
    bool _InputPunch = false;


    private Dictionary<ulong, NetworkObject> itemDictionary;

    public enum ItemType
    {
        None,
        Bomb,
        BlackHole,
        Shoese,
        Star,
        Max
    }

    public enum PlayerNumber
    {
        None = 0,
        Player1,
        Player2,
        Player3,
        Player4
    }


    void Awake()
    {
        itemDictionary = new()
    {
        { (ulong)ItemType.Bomb, _itemBomb.GetComponent<NetworkObject>() },
        { (ulong)ItemType.BlackHole, _itemBlackHole.GetComponent<NetworkObject>() },
        { (ulong)ItemType.Shoese, _itemShouse.GetComponent<NetworkObject>() },
        { (ulong)ItemType.Star, _itemStar.GetComponent<NetworkObject>() }
    };
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;
        _moveSpeedInitial = _moveSpeed;
        _anim = GetComponent<Animator>();


        _ObjectPool = NetworkObjectPool.Instance;

        _dashParticleSystem.Stop();
        _mutekiParticleSystem.Stop(); 
        _shoeseParticleSystem.Stop();

        if (IsServer)
        {
            Invoke("SetRigidFalse", 0.1f);
        }
        else
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; // クライアントでは物理演算しない
        }
        if (IsOwner)
        {
            GetComponent<UICursorToWorld>().SpawnTarget();
            _target = GetComponent<UICursorToWorld>().GetItemTargetTransform().position;
        }
    }

    void SetRigidFalse()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; // クライアントでは物理演算しない
    }

    void FixedUpdate()
    {
        if (IsOwner)
        {
            ItemTargetServerRpc(GetComponent<UICursorToWorld>().GetItemTargetTransform().position);
        }
        if (IsServer)
        {
            //アイテムを持ってるとき
            if (_haveItem != ItemType.None && _haveItem != ItemType.Max)
            {
                UseItem();
            }
            Jump();
            Punch();
            if (!_canNotInputKey)
            {
                Move();
            }
            //移動モーション
            AnimBlendServerRpc(_animBlend);
        }
    }

    void Update()
    {

        if (IsServer)
        {
            if (_item != null)
            {
                _item.transform.position = _haveTrans.position;
                _item.transform.eulerAngles = _haveTrans.eulerAngles;
            }
        }
        //アニメーションブレンド値リセット
        if (_animBlend > 0)
        {
            _animBlend -= 0.1f;
        }

        if (IsOwner)
        {
            //ItemTargetServerRpc(_target);
            //Debug.Log("_target.position : " + _target.position);
            if (!_canNotInputKey)
            {
            //パッド入力
                if (!InputGamePad())
                {
                //キーボード入力
                    InputKeyboard();
                }
                SendInputServerRpc(_InputMove, _InputPunch, _InputUseItem);
                //アイテムターゲット位置更新
                if (_InputUseItem)
                {
                }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            LotteryHaveItem(0.5f);
        }
            }
        }

        UpdateDustEffect();
    }

    //アニメーション
    [ServerRpc(RequireOwnership = false)]
    void AnimBlendServerRpc(float blend)
    {
        _anim.SetFloat("Blend", blend);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimItemServerRpc(bool Item)
    {
        _anim.SetBool("Item", Item);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimDyingServerRpc(bool Dying)
    {
        _anim.SetBool("Dying", Dying);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimPunchServerRpc()
    {
        _anim.SetTrigger("Punch");
    }

    void UpdateDustEffect()
    {
        float speed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;
        bool isMoving = speed > 1.0f;

       // foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        //{
            var em = _dashParticleSystem.emission;
            em.enabled = isMoving;

            //if (isMoving && !ps.isPlaying)
            if (isMoving && !_dashParticleSystem.isPlaying)
            {
               // ps.Play();
                _dashParticleSystem.Play();
            }
            //else if (!isMoving && ps.isPlaying)
            else if (!isMoving && _dashParticleSystem.isPlaying)
            {
                //ps.Stop();
                _dashParticleSystem.Stop();
            }
       // }
    }


    //
    //ゲッター
    //
    public bool GetUseStar()
    {
        return _itemStarUse;
    }
    public PlayerNumber GetPlayerNumber()
    {
        return _playerNumber;
    }
    public int GetPunchDamage()
    {
        return _punchDamage;
    }
    public float GetPunchForce()
    {
        return _punchForce;
    }
    public float GetStunTime()
    {
        return _stunTime;
    }

    public ItemType GetHaveItem()
    {
        return _haveItem;
    }


    //
    //ステータスいじる関係
    //

    //アイテム入手（アイテム抽選時間）
    public void LotteryHaveItem(float itemLotteryTime)
    {
        if (_haveItem == ItemType.None)
        {
            _haveItem = ItemType.Max;
            Invoke("SetHaveItem", itemLotteryTime);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnItemServerRpc(int itemId, Vector3 pos, Quaternion rot)
    {
        GameObject _prefab = null;
        _prefab = itemDictionary[(ulong)_haveItem].gameObject;

        NetworkObject obj = _ObjectPool.Get(_prefab.GetComponent<NetworkObject>(), pos, rot);
        obj.Spawn(true); 
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_prefab.GetComponent<NetworkObject>());

        if(_prefab.GetComponent<Collider>() != null)
        _prefab.GetComponent<Collider>().isTrigger = true;
        if (_prefab.GetComponent<Rigidbody>() != null)
            _prefab.GetComponent<Rigidbody>().isKinematic = true;
        SpawnItemClientRpc(itemId);

        _item = obj.gameObject;
    }

    [ClientRpc]
    void SpawnItemClientRpc(int itemId)
    {
        _haveItem = (ItemType)itemId;
    }
    void SetHaveItem()
    {
        if (!IsServer) return;
        _haveItem = (ItemType)Random.Range((int)ItemType.Bomb, (int)ItemType.Max);
        //_haveItem = ItemType.Bomb;
        AnimItemServerRpc(true);

        //プレイヤーにアイテムを持たせる
        SpawnItemServerRpc((int)_haveItem, _haveTrans.position, _haveTrans.rotation);
    }

    //あべこべ移動速度を逆転させる（何秒後にリセットするか）
    public void MoveSpeedAbekobe(float delay)
    {
        _moveSpeed *= -1;
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度に倍率をかける（かける倍率）
    public void MoveSpeedChange(float dampValue)
    {
        _moveSpeed = _moveSpeedInitial * dampValue;
    }
    //移動速度に倍率をかける（かける倍率,  何秒後にリセットするか）
    public void MoveSpeedChange(float dampValue, float delay)
    {
        //スター効果中は変更しない　靴が優先
        if (_itemShoeseUse) return;
        _moveSpeed = _moveSpeedInitial * dampValue;
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度を初期値に戻す
    public void ResetMoveSpeed()
    {
        //まだ使用中ならリセットしない
        if (_itemShoeseUse || _itemStarUse) return;
        _moveSpeed = _moveSpeedInitial;
    }
    //パンチオブジェクト非アクティブ化
    void PunchActiveFalse()
    {
        ToggleColliderServerRpc(false);
    }
    //パンチクールダウンリセット
    void SetPunchReset()
    {
        _canPunch = true;
    }

    //パンチを受ける(ダメージ, パンチをスタン時間)
    public void ToGetPunch(int damage, float stunTime)
    {
        Stun(stunTime);
        AddDamage(damage);
		
	}

	//スタン（効果時間）
	public void Stun(float delay)
    {
        //スター状態だったら無効
        if (_itemStarUse) return;
        //Debug.Log("受けうつけないお");
        _canNotInputKey = true;
        AnimDyingServerRpc(true);
        _dyingParticleSystem.Play();
        //NetworkEffectSpawner.Instance.PlayEffect(_hitDyingEffectId, transform.position, Quaternion.identity);

		Invoke(nameof(UnlockStun), delay);
    }
    //スタン解除
    void UnlockStun()
    {
        _canNotInputKey = false;

        AnimDyingServerRpc(false);
    }

    void UnlockStar()
    {
        _mutekiParticleSystem.Stop();
        _itemStarUse = false;
    }

    void UnlockShoese()
    {
        _shoeseParticleSystem.Stop();
        _itemShoeseUse = false;
    }
    //ダメージ（受けるダメージ）
    void AddDamage(int damage)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Debug.Log(this.gameObject.name + " is dead.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RotateToMoveDirectionServerRpc(Vector3 dir)
    {
        dir.y = 0.0f;
        if (dir.sqrMagnitude < 0.1f)
            return; // 止まってる時は回転しない

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * 10.0f  // ← 回転速度（数字を上げれば速く振り向く）
        );
    }

    [ServerRpc(RequireOwnership = false)]
    void LinerVelocityServerRpc(Vector3 _InputMove)
    {
        Vector3 _moveVector = Vector3.zero;
        _moveVector = new Vector3(_InputMove.x, 0, _InputMove.y);

        _moveVector.Normalize();
        _moveVector *= _moveSpeed;
        Vector3 input = _moveVector.normalized;
        Vector3 moveDir = input;
        Vector3 vel = _rb.linearVelocity;

        //坂でも原則しない
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f))
        {
            moveDir = Vector3.ProjectOnPlane(input, hit.normal).normalized;
        }

        // 加速・減速（Valorant に近い値）
        float accel = 80.0f;      // 前方向の加速
        float deaccel = 50f;    // 入力を離した時の減速

        // y 以外の現在速度
        Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);

        // 速度更新
        if (input.magnitude > 0.1f)
        {
            // 加速
            horizontalVel = Vector3.MoveTowards(horizontalVel, moveDir * _moveSpeed, accel * Time.fixedDeltaTime);
        }
        else
        {
            // 減速
            horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, deaccel * Time.fixedDeltaTime);
        }
        //_rb.AddForce(dir, ForceMode.Acceleration);
        _rb.linearVelocity = new Vector3(horizontalVel.x, vel.y, horizontalVel.z);
    }

    //
    //MOVE関係
    //
    void Jump()
    {
        if (_FootCollider.GetCanJump())
        {
            if (Input.GetKeyDown(_jump))
            {
                _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            }
        }
    }

    void Move()
    {
        LinerVelocityServerRpc(_InputMove);
        Vector3 _moveVector = Vector3.zero;
        _moveVector = new Vector3(_InputMove.x, 0, _InputMove.y);
        _lookVector = new Vector3(_moveVector.x, 0.0f, _moveVector.z);

        //向き変更
        RotateToMoveDirectionServerRpc(_lookVector);

        //アニメションブレンド更新
        if (_moveVector != Vector3.zero)
        {
            if (_animBlend < 1)
            {
                _animBlend += 0.2f;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ToggleColliderServerRpc(bool state)
    {
        //Debug.Log($"serverRPC punch {state}");
        ToggleColliderClientRpc(state);
    }

    [ClientRpc]
    void ToggleColliderClientRpc(bool state)
    {
        //Debug.Log($"clientRPC punch {state}");
        _punchObj.GetComponent<SphereCollider>().enabled = state;
    }
    void Punch()
    {
        if (_InputPunch && _canPunch)
        {

            AnimPunchServerRpc();

            // 頭の位置からエフェクトを出す
            if (_headPoint != null)
            {
                Vector3 effectPos = transform.position + new Vector3(0f, 0.5f, 0f);

                NetworkEffectSpawner.Instance.PlayEffect(
                    _punchStartEffectId,
                    effectPos,
                    _headPoint.rotation
                );
            }

            ToggleColliderServerRpc(true);

            Invoke(nameof(PunchActiveFalse), _punchDuration);

            _canPunch = false;
            Invoke(nameof(SetPunchReset), _punchDelay);
        }
    }

    //[ServerRpc(RequireOwnership = false)]
    //void ThrowServerRpc(Vector3 velocity)
    //{
    //    Rigidbody rb = _item.GetComponent<Rigidbody>();
    //    rb.linearVelocity = velocity;
    //    rb.isKinematic = false;
    //    Collider col = _item.GetComponent<Collider>();
    //    col.enabled = true;
    //}
    [ClientRpc]
    void ItemTargetClientRpc()
    {
        _target = GetComponent<UICursorToWorld>().CurrentTargetPos;
        ItemTargetServerRpc(_target);
    }

    [ServerRpc(RequireOwnership = false)]
    void ItemTargetServerRpc(Vector3 pos)
    {
        //Debug.Log("OwnerClientId :"+(int)OwnerClientId + "_playerNumber: " + (int)_playerNumber + "ItemTargetServerRpc pos : " + pos);
        _target = pos;
    }


    void UseItem()
    {
        if (_InputUseItem)
        {
            //ItemTargetClientRpc();
            //_target.position = GetComponent<UICursorToWorld>().CurrentTargetPos;
            Debug.Log("_target : " + _target);
            if (_target == Vector3.zero || !_item) return;
            if (_item.GetComponent<Rigidbody>() != null)
            {//投げる系のアイテムはこっち
                // 初速度を計算して付与
                Debug.Log("_itemtrans : " + _item.transform.position);
                Vector3 velocity = CalculateVelocity(_target, _item.transform.position, _itemFlightTime);

                Debug.Log($"velocity{velocity}");
                _item.GetComponent<Item>().ThrowServerRpc(velocity);
            }
            else
            {//使い切りのアイテムはこっち
                _item.GetComponent<PooledNetworkObject>().DestroySelf();
                //靴の効果
                if (_haveItem == ItemType.Shoese)
                {
                    _shoeseParticleSystem.Play();
                    MoveSpeedChange(_itemShoeseChangeSpeed, _itemShoeseDelay);
                    _itemShoeseUse = true;

                    Invoke("UnlockShoese", _itemShoeseDelay);
                }
                //星の硬貨の効果
                else if (_haveItem == ItemType.Star)
                {
                    _mutekiParticleSystem.Play();
                    MoveSpeedChange(_itemStarChangeSpeed, _itemStarDelay);
                    _itemStarUse = true;
                    //一定時間後にスター効果解除
                    Invoke("UnlockStar", _itemStarDelay);
                }
            }
            Debug.Log("UseItem : " + _item);

            _haveItem = ItemType.None;
            SpawnItemClientRpc((int)_haveItem);
            AnimItemServerRpc(false);
            _item = null;
        }
    }

    /// target に time 秒で到達するための初速度を計算
    Vector3 CalculateVelocity(Vector3 target, Vector3 origin, float time)
    {
        Vector3 distance = target - origin;
        Vector3 distanceXZ = new Vector3(distance.x, 0, distance.z);

        float sy = distance.y;
        float sxz = distanceXZ.magnitude;

        Vector3 result = distanceXZ / time; // XZ方向の速度
        result.y = sy / time - 0.5f * Physics.gravity.y * time;

        return result;
    }

    //カメラシェイク用
    public void PlayCameraShake()
    {
        var shaker = ShakeByPerlinNoise.Instance;
        Debug.Log($"{name}: PlayCameraShake (_cameraShake={shaker?.name})");

        if (shaker == null) return;
        shaker.StartShake();
    }

    bool InputKeyboard()
    {
        bool hasInput = false;
        _InputMove = Vector2.zero;
        _InputUseItem = false;
        _InputPunch = false;
        _InputMove = Vector2.zero;
        if (Input.GetKey(_up))
        {
            _InputMove.y += 1;
            hasInput = true;
        }
        if (Input.GetKey(_left))
        {
            _InputMove.x += -1;
            hasInput = true;
        }
        if (Input.GetKey(_down))
        {
            _InputMove.y += -1;
            hasInput = true;
        }
        if (Input.GetKey(_right))
        {
            _InputMove.x += 1;
            hasInput = true;
        }
        if (hasInput)
        {
            _InputMove.Normalize();
        }

        if (Input.GetKeyDown(_useItemKey))
        {
            _InputUseItem = true;
            hasInput = true;
        }
        if (Input.GetKeyDown(_punchKey))
        {
            _InputPunch = true;
            hasInput = true;
        }

        return hasInput;
    }

    //パッドの入力受付、入力がなければfalseを返す
    bool InputGamePad()
    {
        bool hasInput = false;
        _InputMove = Vector2.zero;
        _InputUseItem = false;
        _InputPunch = false;

        var pads = Gamepad.all;
        //自分の番号のパッドを取得
        for (int i = 0; i < pads.Count; i++)
        {
            Gamepad pad = pads[i];
            //if (pad.buttonSouth.wasPressedThisFrame)
            //{
            //    Debug.Log($"Player {i + 1} : A button pressed!");
            //}
        }
        if (pads.Count >= (int)_playerNumber)
        {
            if (pads[(int)_playerNumber - 1] != null)
            {
                gamepad = pads[(int)_playerNumber - 1];
            }
        }
        if (gamepad == null)
        { 
            return hasInput;
        }

        _InputMove = gamepad.leftStick.ReadValue();
        if(_InputMove != Vector2.zero)
        {
              hasInput = true;
        }
        //Debug.Log("Left Stick: " + stick);
        if (gamepad.buttonSouth.wasPressedThisFrame)//A
        {
            _InputUseItem = true;
            hasInput = true;
        }
        if (gamepad.buttonEast.wasPressedThisFrame)//B
        {
            _InputPunch =true;
            hasInput = true;
        }




        return hasInput;
    }

    [ServerRpc]
    void SendInputServerRpc(Vector2 move, bool punch, bool useItem)
    {
        _InputMove = move;
        _InputPunch = punch;
        _InputUseItem = useItem;
    }

}

