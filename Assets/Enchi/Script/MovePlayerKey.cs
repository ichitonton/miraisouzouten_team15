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
    private string thunderLoopTag;


    [System.Serializable]
    public class ItemLotteryEntry
    {
        public ItemType item;
        [Range(0, 100)]
        public int weight; // 抽選の重み
    }

    [System.Serializable]
    public class RankLotteryTable
    {
        public int rank;
        public List<ItemLotteryEntry> items;
    }

    [SerializeField]
    private List<RankLotteryTable> _rankLotteryTables;

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

    [SerializeField] private GameObject _itemThunder;
    [SerializeField] float _thunderDuration = 10.0f;
    [SerializeField] float _thunderStunTime = 5.0f;
    private int _abekobe = 1; //移動速度あべこべ用

    Ranking _ranking;

    [SerializeField] float _itemFlightTime = 2.0f; // 投げるオブジェクトがターゲットに到達するまでの時間

    [SerializeField] ParticleSystem _dashParticleSystem;
    [SerializeField] ParticleSystem _mutekiParticleSystem;
    [SerializeField] ParticleSystem _shoeseParticleSystem;
    [SerializeField] ParticleSystem _dyingParticleSystem;
    [SerializeField] ParticleSystem _thunderParticleSystem;
    [SerializeField] ParticleSystem _thunderHitParticleSystem;
    [SerializeField] ParticleSystem _thunderPiripiriParticleSystem;


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

    Animator _anim;

    //入力受付フラグ
    Vector2 _InputMove = Vector2.zero;
    bool _InputUseItem = false;
    bool _InputPunch = false;
    bool _InputEmote1, _InputEmote2, _InputEmote3, _InputEmote4 = false;
    bool _isEmote = false;

    bool _fly = false;
    [SerializeField] GroundCheck3D _groundCheck;

    //スタン用
    public bool _stun = false;
    public int _hitCount = 0;

    private Dictionary<ulong, NetworkObject> itemDictionary;

    SetSkinMaterial _setSkinMaterial;

    public readonly NetworkVariable<bool> IsRunNet =
    new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool _isRun = true;

    public enum ItemType
    {
        None,
        Bomb,
        BlackHole,
        Shoese,
        Star,
        Thunder,
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
        { (ulong)ItemType.Star, _itemStar.GetComponent<NetworkObject>() },
        { (ulong)ItemType.Thunder, _itemThunder.GetComponent<NetworkObject>() }
    };

        _hitCount = 0;
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        //if (IsServer) return;

        // 初回反映
        _isRun = IsRunNet.Value;

        // 変更監視
        IsRunNet.OnValueChanged += OnIs3DRunChanged;

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;
        _moveSpeedInitial = _moveSpeed;
        _anim = GetComponent<Animator>();
        _setSkinMaterial = GetComponent<SetSkinMaterial>();

        _ObjectPool = NetworkObjectPool.Instance;

        _dashParticleSystem.Stop();
        _mutekiParticleSystem.Stop();
        _shoeseParticleSystem.Stop();

        if (IsServer)
        {
            Invoke("SetRigidFalse", 0.1f);
            _ranking = FindFirstObjectByType<Ranking>();
            if (_ranking == null)
            {
                Debug.LogError("Ranking not found in scene");
            }
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

        thunderLoopTag = "ThunderLoop_" + NetworkObjectId;

    }

    void SetRigidFalse()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; // クライアントでは物理演算しない
    }

    void FixedUpdate()
    {
        if (!_isRun) return;

        if (IsOwner)
        {
            ItemTargetServerRpc(GetComponent<UICursorToWorld>().GetItemTargetTransform().position);
        }
        if (IsServer)
        {
            if (_isEmote)
            {
                _InputMove = Vector2.zero;
                _InputPunch = false;
                _InputUseItem = false;
            }
            else
            {
                AnimatorStateInfo state = _anim.GetCurrentAnimatorStateInfo(0);
                if (_InputMove != Vector2.zero || _InputPunch == true || _InputUseItem == true ||
                    state.IsName("emote1") && state.normalizedTime >= 1.0f ||
                    state.IsName("emote2") && state.normalizedTime >= 1.0f ||
                    state.IsName("emote3") && state.normalizedTime >= 1.0f ||
                    state.IsName("emote4") && state.normalizedTime >= 1.0f)
                {
                    EmoteEnd();
                }
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
                    Emote();
                }
                //if (Input.GetKey((KeyCode)'Q'))
                //{
                //    Debug.Log("あいてむりせっと");
                //    GameStartUseItem();
                //}
            }
             AnimDyingFly(!_groundCheck.CheckGroundStatus());
             //移動モーション
             AnimBlendServerRpc(_animBlend);
        }
    }

    public void GameStartUseItem()
    {
        Debug.Log("あいてむりせっと");
        //アイテムを持ってるとき
        if (_haveItem != ItemType.None && _haveItem != ItemType.Max)
        {
            ItemReset();
        }
    }

    void ItemReset()
    {
        _item.GetComponent<PooledNetworkObject>().DestroySelf();
        SpawnItemClientRpc((int)_haveItem);
        AnimItemServerRpc(false);
        _item = null;
    }

    void AnimDyingFly(bool fly)
    {
        //Debug.Log("fly : " + fly);
        //if (_fly == fly) return;
        AnimDyingFlyServerRpc(fly);
        _fly = fly;
    }

    void Update()
    {

        if (!_isRun) return;

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

                SendInputServerRpc(_InputMove, _InputPunch, _InputUseItem, _InputEmote1, _InputEmote2, _InputEmote3, _InputEmote4);
                if (Input.GetKeyDown(KeyCode.Q))
                {
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
        _anim.SetBool("Fly", Dying); 
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimDyingFlyServerRpc(bool Dying)
    {
        _anim.SetBool("Fly", Dying);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimEmote1ServerRpc(bool Do)
    {
        _anim.SetBool("Emote1", Do);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimEmote2ServerRpc(bool Do)
    {
        _anim.SetBool("Emote2", Do);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimEmote3ServerRpc(bool Do)
    {
        _anim.SetBool("Emote3", Do);
    }
    [ServerRpc(RequireOwnership = false)]
    void AnimEmote4ServerRpc(bool Do)
    {
        _anim.SetBool("Emote4", Do);
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
            PlayEffectDashParticleClientRpc();
        }
        //else if (!isMoving && ps.isPlaying)
        else if (!isMoving && _dashParticleSystem.isPlaying)
        {
            //ps.Stop();
            _dashParticleSystem.Stop();
            StopEffectDashParticleClientRpc();
        }
        // }
    }
    [ClientRpc] void PlayEffectDashParticleClientRpc() { _dashParticleSystem.Play(); }
    [ClientRpc] void StopEffectDashParticleClientRpc() { _dashParticleSystem.Stop(); }

    //
    //ゲッター
    //
    int GetMyTeamId()
    {
        if (!IsServer)
        {
            Debug.LogError("GetMyTeamId called on client!");
            return -1;
        }

        return TeamManager.Instance.GetTeamIdByClientId(OwnerClientId);
    }
    int GetMyRank()
    {
        if (!IsServer)
            return -1;

        if (Ranking.Instance == null)
        {
            Debug.LogError("Ranking.Instance is null");
            return -1;
        }

        int teamId = TeamManager.Instance.GetTeamIdByClientId(OwnerClientId);
        return Ranking.Instance.GetRankByTeamId(teamId);
    }


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
        if (_haveItem == ItemType.None || _haveItem == ItemType.Max)
        {
            Debug.LogError($"Invalid item type: {_haveItem}");
            return;
        }
        _prefab = itemDictionary[(ulong)_haveItem].gameObject;

        NetworkObject obj = _ObjectPool.Get(_prefab.GetComponent<NetworkObject>(), pos, rot);
        obj.Spawn(true);
        obj.GetComponent<PooledNetworkObject>().SetPrefab(_prefab.GetComponent<NetworkObject>());

        if (_prefab.GetComponent<Collider>() != null)
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

        int rank = GetMyRank();
        Debug.Log($"[Lottery] Client={OwnerClientId} Rank={rank}");

        if (rank <= 0)
        {
            Debug.LogError("Invalid Rank, abort lottery");
            return;
        }

        _haveItem = LotteryByRank(rank);
        SpawnItemServerRpc((int)_haveItem, _haveTrans.position, _haveTrans.rotation);
        AnimItemServerRpc(true);
    }
    ItemType LotteryByRank(int rank)
    {
        RankLotteryTable table = _rankLotteryTables.Find(t => t.rank == rank);
        if (table == null || table.items.Count == 0)
        {
            Debug.LogError($"Lottery table not found for rank {rank}");
            return ItemType.None;
        }

        int totalWeight = 0;
        foreach (var item in table.items)
        {
            totalWeight += item.weight;
        }

        int rand = Random.Range(0, totalWeight);
        int current = 0;

        foreach (var item in table.items)
        {
            current += item.weight;
            if (rand < current)
            {
                return item.item;
            }
        }

        return ItemType.None;
    }



    //あべこべ移動速度を逆転させる（何秒後にリセットするか）
    public void MoveSpeedAbekobe(float delay)
    {
        //スター効果中は変更しない　靴が優先
        if (_itemStarUse) return;
        _abekobe = -1;
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
        //if (_itemShoeseUse) return;

        _moveSpeed = Mathf.Max(_moveSpeed, _moveSpeedInitial * dampValue);
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度を初期値に戻す
    public void ResetMoveSpeed()
    {
        _abekobe = 1;
        //まだ使用中ならリセットしない
        if (_itemShoeseUse) return;
        if (_itemStarUse) _moveSpeed = _moveSpeed * _itemStarChangeSpeed;
        else _moveSpeed = _moveSpeedInitial;
        StopThunderEffectClientRpc();

        // 雷効果が切れたらループ停止
        NetworkSoundManager.Instance.StopLoopSfx(
            thunderLoopTag,
            NetworkSoundManager.SoundScope.AllClients
        );

    }

    [ClientRpc]
    void StopThunderEffectClientRpc()
    {
        _thunderHitParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _thunderPiripiriParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
    public bool ToGetPunch(int damage, float stunTime)
    {
        //Stun(stunTime);
        return AddDamage(damage, stunTime);

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
        PlayDyingEffectClientRpc();
        //NetworkEffectSpawner.Instance.PlayEffect(_hitDyingEffectId, transform.position, Quaternion.identity);
        _setSkinMaterial.RequestSetKizetuMaterialServerRpc();

        Invoke(nameof(UnlockStun), delay);
    }
    [ClientRpc]
    void PlayDyingEffectClientRpc() { _dyingParticleSystem.Play(); }
    //スタン解除
    void UnlockStun()
    {
        _canNotInputKey = false;
        _currentHp = _MaxHp;
        _stun = false;
        _hitCount = 0;

        AnimDyingServerRpc(false);
        StopDyingEffectClientRpc();
        _setSkinMaterial.RequestSetNormalMaterialServerRpc();

    }

    [ClientRpc]
    void StopDyingEffectClientRpc()
    { _dyingParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }

    void UnlockStar()
    {
		NetworkSoundManager.Instance.StopLoopSfx("Item_Star", NetworkSoundManager.SoundScope.AllClients);

		_mutekiParticleSystem.Stop();
        StopMutekiEffectClientRpc();
        _itemStarUse = false;
    }
    [ClientRpc]
    void StopMutekiEffectClientRpc()
    { _mutekiParticleSystem.Stop(); }

    void UnlockShoese()
    {
		NetworkSoundManager.Instance.StopLoopSfx("Item_SpeadUP", NetworkSoundManager.SoundScope.AllClients);

		_shoeseParticleSystem.Stop();
        StopShooseEffectClientRpc();
        _itemShoeseUse = false;
    }
    [ClientRpc]
    void StopShooseEffectClientRpc()
    { _shoeseParticleSystem.Stop(); }

    [ServerRpc(RequireOwnership = false)]
    void UseThunderServerRpc()
    {
        foreach (var player in FindObjectsByType<MovePlayerKey>(FindObjectsSortMode.None))
        {
            // 自分以外
            if (player.OwnerClientId == OwnerClientId) continue;
            // 移動反転
            player.MoveSpeedAbekobe(_thunderDuration);
            player.AbekobeClientRpc();
            player.Stun(_thunderStunTime);

        }
    }

    [ClientRpc]
    void AbekobeClientRpc()
    {
        MoveSpeedAbekobe(_thunderDuration);
        _thunderPiripiriParticleSystem.Play();

        // 雷が有効になった瞬間にループ開始（3D）
        NetworkSoundManager.Instance.StartLoopSfx(
            thunderLoopTag,
            NetworkSoundManager.SoundScope.AllClients,
            true,
            transform.position
        );
    }


    [ServerRpc(RequireOwnership = false)]
    void UseThunderEffectServerRpc()
    {
        foreach (var player in FindObjectsByType<MovePlayerKey>(FindObjectsSortMode.None))
        {
            // 自分以外
            if (player.OwnerClientId == OwnerClientId) continue;
            // 雷エフェクト（全クライアント）
            PlayThunderEffectClientRpc(player.GetComponent<NetworkObject>());
			
		}
	}
    [ClientRpc]
    void PlayThunderEffectClientRpc(NetworkObjectReference targetRef)
    {
        if (!targetRef.TryGet(out NetworkObject target))
            return;
        var player = target.GetComponent<MovePlayerKey>();
        if (player == null)
            return;
        player._thunderParticleSystem.Play();
        player._thunderHitParticleSystem.Play();
    }

    //ダメージ（受けるダメージ）
    bool AddDamage(int damage, float stunTime)
    {
        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            Stun(stunTime);
            Debug.Log(this.gameObject.name + " is dead.");
            //死んでいたら
            return true;
        }
        return false;
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
        //if (_FootCollider.GetCanJump())
        //{
        //    if (Input.GetKeyDown(_jump))
        //    {
        //        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        //    }
        //}
    }

    void Move()
    {
        //_InputMove *= _abekobe;
        LinerVelocityServerRpc(_InputMove);
        Vector3 _moveVector = Vector3.zero;
        _moveVector = new Vector3(_InputMove.x, 0, _InputMove.y);
        _lookVector = new Vector3(_InputMove.x, 0.0f, _InputMove.y);

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


                NetworkEffectSpawner.Instance.PlayEffectAttached(_punchStartEffectId, GetComponent<NetworkObject>(), effectPos, _headPoint.rotation);

                //NetworkEffectSpawner.Instance.PlayEffect(
                //    _punchStartEffectId,
                //    effectPos,
                //    _headPoint.rotation
                //);
                //変えた後は戻す
                //NetworkEffectSpawner.Instance._otherRoot = null;
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
					NetworkSoundManager.Instance.StartLoopSfx("Item_SpeadUP", NetworkSoundManager.SoundScope.AllClients, true, transform.position);

					_shoeseParticleSystem.Play();
                    PlayShoeseEffectClientRpc();
                    Invoke("UnlockShoese", _itemShoeseDelay);
                    MoveSpeedChange(_itemShoeseChangeSpeed, _itemShoeseDelay);
                    _itemShoeseUse = true;
                }
                //星の硬貨の効果
                else if (_haveItem == ItemType.Star)
                {
					NetworkSoundManager.Instance.StartLoopSfx("Item_Star", NetworkSoundManager.SoundScope.AllClients, true, transform.position);


					_mutekiParticleSystem.Play();
                    PlayMutekiEffectClientRpc();
                    //一定時間後にスター効果解除
                    Invoke("UnlockStar", _itemStarDelay);
                    MoveSpeedChange(_itemStarChangeSpeed, _itemStarDelay);
                    _itemStarUse = true;
                }
                else if (_haveItem == ItemType.Thunder)
                {
					NetworkSoundManager.Instance.PlaySfx("Item_Rakurai", NetworkSoundManager.SoundScope.AllClients, true, transform.position);


					UseThunderEffectServerRpc();
                    Invoke("UseThunderServerRpc", 0.3f);
                }
            }
            Debug.Log("UseItem : " + _item);

            _haveItem = ItemType.None;
            SpawnItemClientRpc((int)_haveItem);
            AnimItemServerRpc(false);
            _item = null;
        }
    }
    [ClientRpc] void PlayMutekiEffectClientRpc() { _mutekiParticleSystem.Play(); }
    [ClientRpc] void PlayShoeseEffectClientRpc() { _shoeseParticleSystem.Play(); }

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

    void Emote()
    {
        if (_InputEmote1)
        {
            _isEmote = true;
            Invoke("IsEmoteFinish", 1.0f);
            AnimEmote1ServerRpc(true);
            _setSkinMaterial.RequestSetAoriMaterialServerRpc();
        }
        if (_InputEmote2)
        {
            _isEmote = true;
            Invoke("IsEmoteFinish", 1.0f);
            AnimEmote2ServerRpc(true);
            _setSkinMaterial.RequestSetHappyMaterialServerRpc();
        }
        if (_InputEmote3)
        {
            _isEmote = true;
            Invoke("IsEmoteFinish", 1.0f);
            AnimEmote3ServerRpc(true);
            _setSkinMaterial.RequestSetKanasimiMaterialServerRpc();
        }
        if (_InputEmote4)
        {
            _isEmote = true;
            Invoke("IsEmoteFinish", 1.0f);
            AnimEmote4ServerRpc(true);
            _setSkinMaterial.RequestSetOkoriMaterialServerRpc();
        }
    }

    void IsEmoteFinish()
    {
        _isEmote = false;
    }

    void EmoteEnd()
    {
        AnimEmote1ServerRpc(false);
        AnimEmote2ServerRpc(false);
        AnimEmote3ServerRpc(false);
        AnimEmote4ServerRpc(false);
        _setSkinMaterial.RequestSetNormalMaterialServerRpc();
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
        _InputEmote1 = false;
        _InputEmote2 = false;
        _InputEmote3 = false;
        _InputEmote4 = false;

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
        if (_InputMove != Vector2.zero)
        {
            hasInput = true;
        }
        //Debug.Log("Left Stick: " + stick);
        if (gamepad.rightShoulder.isPressed || gamepad.rightTrigger.isPressed)//R
        {
            _InputUseItem = true;
            hasInput = true;
        }
        if (gamepad.aButton.isPressed || gamepad.bButton.isPressed)//
        {
            _InputPunch = true;
            hasInput = true;
        }
        if (gamepad.dpad.up.isPressed)
        {
            Debug.Log("Dpad Up pressed");
            _InputEmote1 = true;
            hasInput = true;
        }
        if (gamepad.dpad.down.isPressed)
        {
            Debug.Log("Dpad Down pressed");
            _InputEmote2 = true;
            hasInput = true;
        }
        if (gamepad.dpad.left.isPressed)
        {
            Debug.Log("Dpad Left pressed");
            _InputEmote3 = true;
            hasInput = true;
        }
        if (gamepad.dpad.right.isPressed)
        {
            Debug.Log("Dpad Right pressed");
            _InputEmote4 = true;
            hasInput = true;
        }





        return hasInput;
    }

    [ServerRpc]
    void SendInputServerRpc(Vector2 move, bool punch, bool useItem, bool emote1, bool emote2, bool emote3, bool emote4)
    {

        _InputMove = move * _abekobe;
        _InputPunch = punch;
        _InputUseItem = useItem;
        _InputEmote1 = emote1;
        _InputEmote2 = emote2;
        _InputEmote3 = emote3;
        _InputEmote4 = emote4;
    }

    private void OnIs3DRunChanged(bool prev, bool next)
    {
        _isRun = next;
    }

}

