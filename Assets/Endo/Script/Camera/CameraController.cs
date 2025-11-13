using NUnit.Framework;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{

    [SerializeField] private GameObject _GameManager;
    private GameObject _player1;
    private GameObject _player2;

    private List<GameObject> _players = new List<GameObject>();
    [SerializeField] private PlayerNetworkConnect _playerNetworkConnect = default;    

    [Header("カメラ設定")]
    [SerializeField] private Vector3 _offset = new Vector3(0, 15, -15);
    [SerializeField] private float _smoothSpeed = 5f;//追従の滑らかさ
    

    // ズーム関連
    [SerializeField] private float _minZoom = 10f;   // 最小距離
    [SerializeField] private float _maxZoom = 30f;   // 最大距離
    [SerializeField] private float _zoomLimiter = 10f; // 距離に応じた倍率
    [SerializeField] private float _zoomSpeed = 10.0f;
    [SerializeField] private float _zoomView = 10.0f;

    private Camera _cam;


    [Header("角度制御")]
    [SerializeField] private float _rotateSpeed = 10.0f;
    private float _pitch = 0f;//縦方向の回転(z軸)
    private void Start()
    {
        _cam = GetComponent<Camera>();
        var nm = NetworkManager.Singleton;

        Debug.Log(NetworkManager.Singleton);

        //ローカルネットワークに接続したとき
        nm.OnClientConnectedCallback += OnClientConnected;
        
    }
    // Update is called once per frame
    private void LateUpdate()
    {
        

        if(GameManager.Instance._IsLanModeActive == true)
        {
            //Debug.Log("カメラの処理をオンライン用に切り替えます");
            /*Debug.Log(GameManager.Instance._objectList.Count);
            foreach (var obj in GameManager.Instance._objectList)
            {
                Debug.Log(obj.name);
            }*/
            var players = GameManager.Instance._networkObjectList;

            Debug.Log("ネットワークオブジェクトの数 = " + players.Count);
            Debug.Log(_players.Count);
        }
        else
        {
            _players.Clear();
            //プレイヤーを登録
            foreach(var ob in GameObject.FindGameObjectsWithTag("Player"))
            {
                _players.Add(ob);
            }
           
        }

        //プレイヤーがいないなら処理中止
        if (_players.Count == 0)
            return;

        //二人いないなら
        if (_players.Count == 1)
        {
            //Debug.LogError("プレイヤー二人いねーよ");
            //Debug.Log("プレイヤー二人いないから繋げれねーってばよ");
            SinglePlayer();
        }
        else if (_players.Count == 2)
        {
            PairPlayer();
        }
    }


    private void PairPlayer()
    {

        _player1 = _players[0];
        _player2 = _players[1];

        if (_player1 == null||_player2 == null) return;

        //キー入力でピッチ角を変更
        if (Input.GetKey(KeyCode.UpArrow))
            _pitch += _rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            _pitch -= _rotateSpeed * Time.deltaTime;

        // クランプ（角度制限、真上から真横までぐらい）
        _pitch = Mathf.Clamp(_pitch, -30f, 30f);

        //Debug.Log(_pitch);

        //プレイヤー間の真ん中を見る
        Vector3 center = (_player1.transform.position + _player2.transform.position) / 2f;

        //ピッチ角を offset に反映
        Quaternion rotation = Quaternion.Euler(_pitch, 0f, 0f);
        Vector3 rotatedOffset = rotation * _offset;


        //カメラの位置
        Vector3 targetPosition = center + rotatedOffset;

        //追従を線形補完で滑らかに
        transform.position = Vector3.Lerp(transform.position, targetPosition, _smoothSpeed * Time.deltaTime);

        // プレイヤー間の距離からターゲットズームを算出
        float distance = Vector3.Distance(_player1.transform.position, _player2.transform.position);
        float targetZoom = Mathf.Lerp(_minZoom, _maxZoom, distance / _zoomLimiter);

        // 見切れチェック
        Vector3 viewPos1 = _cam.WorldToViewportPoint(_player1.transform.position);
        Vector3 viewPos2 = _cam.WorldToViewportPoint(_player2.transform.position);


        // 広め（ズームアウト判定）
        float outerBorder = 0.10f;
        bool outsideP1 = (viewPos1.x < outerBorder || viewPos1.x > 1f - outerBorder ||
                          viewPos1.y < outerBorder || viewPos1.y > 1f - outerBorder);
        bool outsideP2 = (viewPos2.x < outerBorder || viewPos2.x > 1f - outerBorder ||
                          viewPos2.y < outerBorder || viewPos2.y > 1f - outerBorder);

        // スムーズに補間
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetZoom, Time.deltaTime * _zoomSpeed);

        //見る位置
        center.y -= 1f;
        transform.LookAt(center);

    }

    private void SinglePlayer()
    {
        _player1 = _players[0];

        if (_player1 == null) return;

        //キー入力でピッチ角を変更
        if (Input.GetKey(KeyCode.UpArrow))
            _pitch += _rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            _pitch -= _rotateSpeed * Time.deltaTime;

        // クランプ（角度制限、真上から真横までぐらい）
        _pitch = Mathf.Clamp(_pitch, -30f, 30f);

        //Debug.Log(_pitch);

        //プレイヤー間の真ん中を見る
        Vector3 center = _player1.transform.position;

        //ピッチ角を offset に反映
        Quaternion rotation = Quaternion.Euler(_pitch, 0f, 0f);
        Vector3 rotatedOffset = rotation * (_offset / 2f);


        //カメラの位置
        Vector3 targetPosition = center + rotatedOffset;

        //追従を線形補完で滑らかに
        transform.position = Vector3.Lerp(transform.position, targetPosition, _smoothSpeed * Time.deltaTime);

        //見る位置
        center.y -= 1f;
        transform.LookAt(center);
    }


    private void OnClientConnected(ulong clientId)
    {
        //ローカル内に接続したときに再登録
        StartCoroutine(DelayRegisterPlayer());
    }

    private IEnumerator DelayRegisterPlayer()
    {

        yield return new WaitForSeconds(_playerNetworkConnect._delayTime + 0.3f);
        //Debug.Log("カメラが追うプレイヤーを再登録します");
        RegisterPlayer();

    }

    private void RegisterPlayer()
    {
        _players.Clear();//一回リセット


        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。
            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if(playerObj.gameObject.CompareTag("Player") && playerObj.IsOwner)
                {
                    //Debug.Log("所有権を持ったプレイヤーです");
                    _players.Add(playerObj.gameObject);
                }
            }


        }
    }

}
