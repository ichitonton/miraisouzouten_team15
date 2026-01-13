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
    [SerializeField] Transform _haveTrans;//持ってるアイテム
    Transform _target;
    [SerializeField] float _itemFlightTime = 2.0f;

    //エフェクト関連
    [SerializeField] GameObject _effDash_2;
    [SerializeField] GameObject _eff_HitPunch;
    [SerializeField] Transform _headPoint;
    private int _punchStartEffectId = 3;

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
    private GameObject _item;
    GameObject _pool = null;

    float _animBlend = 0.0f;

    // ★ここが変更ポイント：Gamepad.all で拾わない
    private Gamepad gamepad;

    Vector3 _lookVector = Vector3.zero;

    [SerializeField] CanJump _FootCollider;

    Animator _anim;

    // ★追加：このプレイヤー専用のPad情報
    [Header("Pad Binding (required)")]
    [SerializeField] private PlayerPadBinding _padBinding;

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

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        PunchActiveFalse();
        _currentHp = _MaxHp;
        _moveSpeedInitial = _moveSpeed;
        _anim = GetComponent<Animator>();

        if (_punchObj != null) _punchObj.SetActive(false);
        _pool = GameObject.Find("ItemObjectPool");

        // ★Binding自動取得（付け忘れ防止）
        if (_padBinding == null) _padBinding = GetComponent<PlayerPadBinding>();
    }

    void Update()
    {
        // ★毎フレーム：自分のBindingからpadを取得（Gamepad.all順番は一切見ない）
        gamepad = (_padBinding != null) ? _padBinding.GetPadOrNull() : null;

        if (_item != null)
        {
            _item.transform.position = _haveTrans.position;
            _item.transform.eulerAngles = _haveTrans.eulerAngles;
        }

        if (_animBlend > 0) _animBlend -= 0.1f;

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

        if (_anim != null) _anim.SetFloat("Blend", _animBlend);

        UpdateDustEffect();
    }

    private bool PadOK()
    {
        return gamepad != null && gamepad.added && gamepad.enabled;
    }

    void UpdateDustEffect()
    {
        float speed = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z).magnitude;
        bool isMoving = speed > 1.0f;

        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var em = ps.emission;
            em.enabled = isMoving;

            if (isMoving && !ps.isPlaying) ps.Play();
            else if (!isMoving && ps.isPlaying) ps.Stop();
        }
    }

    public PlayerNumber GetPlayerNumber() => _playerNumber;
    public int GetPunchDamage() => _punchDamage;
    public float GetPunchForce() => _punchForce;
    public float GetStunTime() => _stunTime;
    public ItemType GetHaveItem() => _haveItem;

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

        for (int i = 0; i < _pool.transform.childCount; i++)
        {
            GameObject _kari = _pool.transform.GetChild(i).gameObject;
            if (_kari.GetComponent<ItemBomb>() != null && !_kari.activeSelf)
            {
                _kari.gameObject.SetActive(true);
                _kari.transform.position = _haveTrans.transform.position;
                _kari.transform.rotation = transform.rotation;

                _item = _kari.gameObject;
                _item.transform.SetParent(transform);
                break;
            }
        }

        _item.GetComponent<Collider>().enabled = false;
        _item.GetComponent<Rigidbody>().isKinematic = true;
    }

    public void MoveSpeedAbekobe(float delay)
    {
        _moveSpeed *= -1;
        Invoke("ResetMoveSpeed", delay);
    }

    public void MoveSpeedChange(float dampValue)
    {
        _moveSpeed = _moveSpeedInitial * dampValue;
    }
    public void MoveSpeedChange(float dampValue, float delay)
    {
        _moveSpeed = _moveSpeedInitial * dampValue;
        Invoke("ResetMoveSpeed", delay);
    }

    public void ResetMoveSpeed()
    {
        _moveSpeed = _moveSpeedInitial;
    }

    void PunchActiveFalse()
    {
        if (_punchObj != null) _punchObj.SetActive(false);
    }

    void SetPunchReset()
    {
        _canPunch = true;
    }

    public void ToGetPunch(int damage, float stunTime)
    {
        Stun(stunTime);
        AddDamage(damage);
    }

    public void Stun(float delay)
    {
        _canNotInputKey = true;
        _anim.SetBool("Dying", true);
        Invoke(nameof(UnlockStun), delay);
    }

    void UnlockStun()
    {
        _canNotInputKey = false;
        _anim.SetBool("Dying", false);
    }

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
        if (dir.sqrMagnitude < 0.1f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10.0f);
    }

    void LinerVelocityServerRpc(Vector3 dir)
    {
        _rb.linearVelocity = dir;
    }

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

        // ★Pad入力：PadOK() のときだけ読む（抜き差し例外防止）
        if (PadOK())
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            _moveVector.x += stick.x;
            _moveVector.z += stick.y;
        }

        if (Input.GetKey(_up)) _moveVector.z += 1;
        if (Input.GetKey(_left)) _moveVector.x += -1;
        if (Input.GetKey(_down)) _moveVector.z += -1;
        if (Input.GetKey(_right)) _moveVector.x += 1;

        _moveVector.Normalize();
        _moveVector *= _moveSpeed;

        Vector3 input = _moveVector.normalized;
        Vector3 moveDir = input;
        Vector3 vel = _rb.linearVelocity;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f))
        {
            moveDir = Vector3.ProjectOnPlane(input, hit.normal).normalized;
        }

        float accel = 80.0f;
        float deaccel = 50f;

        Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);

        if (input.magnitude > 0.1f)
        {
            horizontalVel = Vector3.MoveTowards(horizontalVel, moveDir * _moveSpeed, accel * Time.fixedDeltaTime);
        }
        else
        {
            horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, deaccel * Time.fixedDeltaTime);
        }

        if (_rb.linearVelocity.sqrMagnitude < _moveSpeed * _moveSpeed)
            LinerVelocityServerRpc(new Vector3(horizontalVel.x, vel.y, horizontalVel.z));

        _lookVector = new Vector3(_moveVector.x, 0.0f, _moveVector.z);
        RotateToMoveDirectionServerRpc(_lookVector);

        if (_moveVector != Vector3.zero)
        {
            if (_animBlend < 1) _animBlend += 0.2f;
        }
    }

    void Punch()
    {
        bool padPunch = PadOK() && gamepad.buttonSouth.wasPressedThisFrame;
        bool keyPunch = Input.GetKeyDown(_punch);

        if ((keyPunch || padPunch) && _canPunch)
        {
            _anim.SetTrigger("Punch");

            if (_headPoint != null)
            {
                Vector3 effectPos = transform.position + new Vector3(0f, 0.5f, 0f);

                NetworkEffectSpawner.Instance.PlayEffect(
                    _punchStartEffectId,
                    effectPos,
                    _headPoint.rotation
                );
            }

            if (_punchObj != null) _punchObj.SetActive(true);
            Invoke(nameof(PunchActiveFalse), _punchDuration);

            _canPunch = false;
            Invoke(nameof(SetPunchReset), _punchDelay);
        }
    }

    void UseItem()
    {
        bool padUse = PadOK() && gamepad.buttonEast.isPressed;
        bool keyUse = Input.GetKeyDown(_useItem);

        if (keyUse || padUse)
        {
            if (_item == null) return;

            _item.GetComponent<Collider>().enabled = true;
            _item.GetComponent<Rigidbody>().isKinematic = false;

            _item.transform.SetParent(_pool.transform);
            _item.transform.position = transform.position + transform.up * 2.5f;

            if (!_target || !_item) return;
            Rigidbody rb = _item.GetComponent<Rigidbody>();

            Vector3 velocity = CalculateVelocity(_target.position, _item.transform.position, _itemFlightTime);
            rb.linearVelocity = velocity;

            _haveItem = ItemType.None;
            _anim.SetBool("ItemBomb", false);
            _item = null;
        }

        Vector3 CalculateVelocity(Vector3 target, Vector3 origin, float time)
        {
            Vector3 distance = target - origin;
            Vector3 distanceXZ = new Vector3(distance.x, 0, distance.z);

            float sy = distance.y;

            Vector3 result = distanceXZ / time;
            result.y = sy / time - 0.5f * Physics.gravity.y * time;

            return result;
        }
    }

    public void PlayCameraShake()
    {
        var shaker = ShakeByPerlinNoise.Instance;
        if (shaker == null) return;
        shaker.StartShake();
    }
}
