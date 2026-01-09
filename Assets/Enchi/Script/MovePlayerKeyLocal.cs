using NUnit.Framework.Constraints;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.EventSystems.StandaloneInputModule;

public class MovePlayerKeyLocal : MonoBehaviour
{
    [SerializeField] KeyCode _up;
    [SerializeField] KeyCode _down;
    [SerializeField] KeyCode _left;
    [SerializeField] KeyCode _right;
    [SerializeField] KeyCode _jump;
    [SerializeField] KeyCode _punch;
    [SerializeField] KeyCode _useItem;

    [SerializeField] float _moveSpeed = 7.0f;
    [SerializeField] float _jumpForce = 7.0f;

    [SerializeField] float _punchDuration = 0.5f;
    [SerializeField] float _punchDelay = 0.5f;
    [SerializeField] GameObject _punchObj;
    [SerializeField] float _toGetPunchTime = 0.5f;
    [SerializeField] int _MaxHp = 100;
    [SerializeField] int _punchDamage = 20;
    [SerializeField] float _punchForce = 10.0f;
    [SerializeField] float _stunTime = 1.0f;//パンチした時のスタン時間
    //[SerializeField] GameObject _itemObj; // 投げるオブジェクト
    [SerializeField] Transform _haveTrans;//持ってるアイテム
    Transform _target;
    [SerializeField] float _itemFlightTime = 2.0f; // 投げるオブジェクトがターゲットに到達するまでの時間

    //エフェクト関連
    [SerializeField] GameObject _effDash_2; // 移動中エフェクト
    [SerializeField] GameObject _eff_HitPunch; // パンチダメージエフェクト
    [SerializeField] Transform _headPoint; //頭の位置
    private int _punchStartEffectId = 3;   //頭のエフェクト

    [SerializeField] PlayerNumber _playerNumber = PlayerNumber.None;

    GameObject _effDash2Instance;
    ParticleSystem _effDash2Ps;

    //カメラシェイク
    //[SerializeField] ShakeByPerlinNoise _cameraShake;

    Rigidbody _rb;

    private Vector3 _moveDir;
    private Vector3 _lastMoveDir;
    private bool _canNotInputKey = false;
    private int _currentHp;
    private float _moveSpeedInitial;
    private bool _canPunch = true;
    private ItemType _haveItem = ItemType.None;
    private GameObject _item;
    GameObject _pool = null;

    float _animBlend = 0.0f;

    Gamepad gamepad;

    Vector3 _lookVector = Vector3.zero;//向いている方向

    [SerializeField] CanJump _FootCollider;

    Animator _anim;

    public enum ItemType
    {
        None,
        Bomb,
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



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;
        _moveSpeedInitial = _moveSpeed;
        _anim = GetComponent<Animator>();

        //シェイク用カメラ自動取得
        //if (_cameraShake == null)
        //{
        //    var cam = Camera.main;
        //    if (cam != null)
        //    {
        //        _cameraShake = cam.GetComponent<ShakeByPerlinNoise>();
        //    }
        //}
        _punchObj.SetActive(false);


        _pool = GameObject.Find("ItemObjectPool");
    }


    // Update is called once per frame
    void Update()
    {
        if (_item != null)
        {
            _item.transform.position = _haveTrans.position;
            _item.transform.eulerAngles = _haveTrans.eulerAngles;
        }
        //Debug.Log(_haveItem);
        if (_animBlend > 0)
        {
            _animBlend -= 0.1f;
        }
        {
            var pads = Gamepad.all;

            for (int i = 0; i < pads.Count; i++)
            {
                Gamepad pad = pads[i];
                if (pad.buttonSouth.wasPressedThisFrame)
                {
                    Debug.Log($"Player {i + 1} : A button pressed!");
                }
            }
            if (pads.Count >= (int)_playerNumber)
            {
                if (pads[(int)_playerNumber - 1] != null)
                {
                    gamepad = pads[(int)_playerNumber - 1];
                }
            }
            if (_haveItem != ItemType.None && _haveItem != ItemType.Max)
            {
                UseItem();
            }
            if (!_canNotInputKey)
            {
                Jump();
                Punch();
                Move();
            }
            //_anim.linearVelocityBlending = true;
            _anim.SetFloat("Blend", _animBlend);
        }

        UpdateDustEffect();
    }

    void UpdateDustEffect()
    {
        float speed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;
        bool isMoving = speed > 1.0f;

        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var em = ps.emission;
            em.enabled = isMoving;

            if (isMoving && !ps.isPlaying)
            {
                ps.Play();
            }
            else if (!isMoving && ps.isPlaying)
            {
                ps.Stop();
            }
        }
    }


    //
    //ゲッター
    //
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

    void SetHaveItem()
    {
        _haveItem = (ItemType)Random.Range((int)ItemType.Bomb, (int)ItemType.Max);
        _anim.SetBool("ItemBomb", true);

        //bool _isChild = false;

        //プレイヤーにアイテムを持たせる
        for (int i = 0; i < _pool.transform.childCount; i++)
        {
            //非アクティブの子オブジェクト検索
            GameObject _kari = _pool.transform.GetChild(i).gameObject;
            if (_kari.GetComponent<ItemBomb>() != null &&
                !_kari.activeSelf)
            {
                _kari.gameObject.SetActive(true);
                _kari.transform.position = _haveTrans.transform.position;
                _kari.transform.rotation = transform.rotation;

                _item = _kari.gameObject;

                //_isChild = true;
                _item.transform.SetParent(transform);
                break;
            }
        }

        //子オブジェクトが足りなければ新規作成
        //if (!_isChild)
        //_item = Instantiate(_itemObj, _haveTrans.transform.position, transform.rotation, transform);

        _item.GetComponent<Collider>().enabled = false;
        _item.GetComponent<Rigidbody>().isKinematic = true;

        //アイテムプール内で更新をかけて、非アクティブオブジェクトが不足しているときに新規作成

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
        _moveSpeed = _moveSpeedInitial * dampValue;
        Invoke("ResetMoveSpeed", delay);
    }

    //移動速度を初期値に戻す
    public void ResetMoveSpeed()
    {
        _moveSpeed = _moveSpeedInitial;
    }
    //パンチオブジェクト非アクティブ化
    void PunchActiveFalse()
    {
        _punchObj.SetActive(false);
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
        Debug.Log("受けうつけないお");
        _canNotInputKey = true;
        _anim.SetBool("Dying", true);

        Invoke(nameof(UnlockStun), delay);
    }
    //スタン解除
    void UnlockStun()
    {
        _canNotInputKey = false;

        _anim.SetBool("Dying", false);
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
    void LinerVelocityServerRpc(Vector3 dir)
    {
        _rb.linearVelocity = dir;
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
        Vector3 _moveVector = Vector3.zero;
        //Vector3 _lookVector = Vector3.zero;
        bool hasInput = false;

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            Debug.Log("Left Stick: " + stick);

            _moveVector.x += stick.x;
            _moveVector.z += stick.y;
        }


        if (Input.GetKey(_up))
        {
            _moveVector.z += 1;
            hasInput = true;
        }
        if (Input.GetKey(_left))
        {
            _moveVector.x += -1;
            hasInput = true;
        }
        if (Input.GetKey(_down))
        {
            _moveVector.z += -1;
            hasInput = true;
        }
        if (Input.GetKey(_right))
        {
            _moveVector.x += 1;
            hasInput = true;
        }

        if (_effDash2Instance)
        {
            Vector3 backPos = transform.position
                              - transform.forward * 0.5f;

            _effDash2Instance.transform.position = backPos;
        }

        //エフェクトの位置更新
        if (_effDash2Instance)
        {
            Vector3 backPos = transform.position
                              - transform.forward * 0.5f;

            _effDash2Instance.transform.position = backPos;
        }

        _moveVector.Normalize();
        _moveVector *= _moveSpeed;
        // _moveVector.y = _rb.linearVelocity.y;
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
        float maxSpeed = 7f;    // 走り速度


        //Vector3 vel = _rb.linearVelocity;

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
        ////transform.LookAt(transform.position + new Vector3(_moveVector.x, 0, _moveVector.z));
        if (_rb.linearVelocity.sqrMagnitude < _moveSpeed * _moveSpeed)
            LinerVelocityServerRpc(new Vector3(horizontalVel.x, vel.y, horizontalVel.z));

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
    void Punch()
    {
        if ((Input.GetKeyDown(_punch) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)) && _canPunch)
        {

            _anim.SetTrigger("Punch");

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

            _punchObj.SetActive(true);

            Invoke(nameof(PunchActiveFalse), _punchDuration);

            _canPunch = false;
            Invoke(nameof(SetPunchReset), _punchDelay);
        }
    }


    void UseItem()
    {
        if (Input.GetKeyDown(_useItem) || gamepad.buttonEast.isPressed)
        {

            _item.GetComponent<Collider>().enabled = true;
            _item.GetComponent<Rigidbody>().isKinematic = false;

            _item.transform.SetParent(_pool.transform);

            _item.transform.position = transform.position + transform.up * 2.5f;

            if (!_target || !_item) return;
            Rigidbody rb = _item.GetComponent<Rigidbody>();

            // 初速度を計算して付与
            Vector3 velocity = CalculateVelocity(_target.position, _item.transform.position, _itemFlightTime);
            rb.linearVelocity = velocity;

            _haveItem = ItemType.None;
            _anim.SetBool("ItemBomb", false);
            _item = null;
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

    }

    //カメラシェイク用
    public void PlayCameraShake()
    {
        var shaker = ShakeByPerlinNoise.Instance;
        Debug.Log($"{name}: PlayCameraShake (_cameraShake={shaker?.name})");

        if (shaker == null) return;
        shaker.StartShake();
    }

}
