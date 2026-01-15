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
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 30f;
    private float _pitch = 0f;//縦方向の回転(z軸)

    // ピッチに応じて LookAt の高さを変える
    [Header("注視点( LookAt ) 高さ制御")]
    [SerializeField] private float _lookAtBaseHeight = 0.0f;     // 常に足す基準の高さ
    [SerializeField] private float _lookAtHeightSideView = 2.0f; // 横から見てる時にどれだけ上を見るか
    [SerializeField] private float _lookAtHeightTopView = -1.0f;// 俯瞰に近い時にどれだけ下を見るか



    [Header("ズーム入力")]
    [SerializeField] private KeyCode _zoomInKey = KeyCode.B;   // ズームイン
    [SerializeField] private KeyCode _zoomOutKey = KeyCode.V;  // ズームアウト
    [SerializeField] private float _manualZoomSpeed = 20f;     // 手動ズームの速さ
    [SerializeField] private float _maxManualOffset = 10f;     // 自動ズームからどれだけズラせるか
    private float _manualZoomOffset = 0f;                      // 手動オフセット

    // オブジェクトを透明にしたいよ
	[Header("カメラ障害物フェード")]
	[SerializeField] private LayerMask _obstacleMask;   // 壁・柱とかのレイヤーを指定
	[SerializeField] private int _maxObstacleHits = 16; // 1フレームの最大ヒット数

	// 今フェード中のオブジェクトたち
	private readonly List<CameraObstacleFader> _fadingNow = new List<CameraObstacleFader>();
	private RaycastHit[] _obstacleHits;

    [Header("Delay")]
    private float _delayTime = 0.5f;

	private void Start()
    {
        _cam = GetComponent<Camera>();
        var nm = NetworkManager.Singleton;

		_obstacleHits = new RaycastHit[_maxObstacleHits];

		Debug.Log(NetworkManager.Singleton);

        //ローカルネットワークに接続したとき
        nm.OnClientConnectedCallback += OnClientConnected;
        
    }
    // Update is called once per frame
    private void LateUpdate()
    {

        // まずズーム入力を読む
        HandleZoomInput();

        if (NetworkManager.Singleton == null) return;
        if(GameManager.Instance == null) return;

        if (GameManager.Instance._IsLanModeActive == true)
        {
            //Debug.Log("カメラの処理をオンライン用に切り替えます");
            /*Debug.Log(GameManager.Instance._objectList.Count);
            foreach (var obj in GameManager.Instance._objectList)
            {
                Debug.Log(obj.name);
            }*/
            var players = GameManager.Instance._networkObjectList;

            if(players.Count > 0) 
            {
                    Debug.Log("ネットワークオブジェクトの数 = " + players.Count);
            }
            //Debug.Log(_players.Count);

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

		HandleObstacleFadeForPlayers();
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
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

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

        // プレイヤー間の距離から「自動ズーム値」を算出
        float distance = Vector3.Distance(_player1.transform.position, _player2.transform.position);

        // まず 0 1 にクランプ
        float t = Mathf.Clamp01(distance / _zoomLimiter);

        // 0→_minZoom, 1→_maxZoom の範囲内に収まる
        float baseZoom = Mathf.Lerp(_minZoom, _maxZoom, t);

        // 手動オフセットを足して最終ターゲットズームに
        float targetZoom = Mathf.Clamp(baseZoom + _manualZoomOffset, _minZoom, _maxZoom);

        // スムーズに補間
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetZoom, Time.deltaTime * _zoomSpeed);

        //見る位置
        center.y -= 1f;
        // 見る位置（ピッチに応じて高さを調整）
        Vector3 lookAtPos = GetLookAtPosition(center);
        //Debug.Log("今見ている位置は" + lookAtPos);
        transform.LookAt(lookAtPos);

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
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

        //Debug.Log(_pitch);

        //プレイヤー間の真ん中を見る
        Vector3 center = _player1.transform.position;

        //ピッチ角を offset に反映
        Quaternion rotation = Quaternion.Euler(_pitch, 0f, 0f);
        Vector3 rotatedOffset = rotation * (_offset / 2f);
        //Vector3 rotatedOffset = GetRotatedOffset(0.5f);


        //カメラの位置
        Vector3 targetPosition = center + rotatedOffset;

        //追従を線形補完で滑らかに
        transform.position = Vector3.Lerp(transform.position, targetPosition, _smoothSpeed * Time.deltaTime);

        // スムーズに補間
        float baseZoom = 35f; // 1人のときの基準ズーム（好みで変えてOK）
        float targetZoom = Mathf.Clamp(baseZoom + _manualZoomOffset, _minZoom, _maxZoom);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetZoom, Time.deltaTime * _zoomSpeed);

        //Debug.Log("カメラのビュー" +  _cam.fieldOfView);
        //Debug.Log("マニュアル" + _manualZoomOffset);

        //見る位置
        //center.y -= 1f;

        // 見る位置（ピッチに応じて高さを調整）
        Vector3 lookAtPos = GetLookAtPosition(center);
        //Debug.Log("今見ている位置は" + lookAtPos);
        transform.LookAt(lookAtPos);
    }


    private void OnClientConnected(ulong clientId)
    {
        //ローカル内に接続したときに再登録
        StartCoroutine(DelayRegisterPlayer());
    }

    private IEnumerator DelayRegisterPlayer()
    {

        yield return new WaitForSeconds(_playerNetworkConnect._delayTime + _delayTime);
        //Debug.Log("カメラが追うプレイヤーを再登録します");
        RegisterPlayer();

    }


    private void HandleZoomInput()
    {
        float dir = 0f;

        if (Input.GetKey(_zoomInKey)) dir -= 1f;  // FOVを小さく = ズームイン
        if (Input.GetKey(_zoomOutKey)) dir += 1f;  // FOVを大きく = ズームアウト

        if (Mathf.Abs(dir) > 0.01f)
        {
            _manualZoomOffset += dir * _manualZoomSpeed * Time.deltaTime;
            _manualZoomOffset = Mathf.Clamp(_manualZoomOffset, -_maxManualOffset, _maxManualOffset);
        }
    }

    /// <summary>
    /// 現在のピッチ角に応じて、LookAt する位置の高さを調整した center を返す
    /// 「横から見るほどプレイヤーの上あたりを見る」イメージ
    /// </summary>
    private Vector3 GetLookAtPosition(Vector3 center)
    {
        // forward.y の絶対値が小さいほど「横から見ている」状態
        // forward.y の絶対値が大きいほど「俯瞰・真上」状態
        float vertical = Mathf.Abs(_cam.transform.forward.y);
        // vertical = 0 → 完全に横から
        // vertical = 1 → 真上/真下（※今回は真下には行かないはずだけど）

        // 横 view: vertical ≒ 0 → t = 1
        // 上 view : vertical ≒ 1 → t = 0
        float t = Mathf.InverseLerp(1f, 0f, vertical);

        // t=0 → _lookAtOffsetTopView
        // t=1 → _lookAtOffsetSideView
        float offsetY = Mathf.Lerp(_lookAtHeightTopView, _lookAtHeightSideView, t);

        center.y += offsetY;
        return center;
    }

    private void RegisterPlayer()
    {
        _players.Clear();//一回リセット

        Debug.Log("カメラに映すプレイヤーを登録");

        Debug.Log("ネットワークオブジェクトの数" + GameManager.Instance._networkObjectList.Count);

        foreach (var playerRef in GameManager.Instance._networkObjectList)
        {
            //これで「実際に存在するネットワークオブジェクトを取り出す」処理。
            //成功した場合 playerObj に GameObject が入る。

            if (playerRef.TryGet(out var playerObj))
            {
                //プレイヤーのタグを持っているかつ所有権があるなら
                if(playerObj.gameObject.CompareTag("Player") && playerObj.IsOwner)
                {
                    Debug.Log("所有権を持ったプレイヤーです");
                    _players.Add(playerObj.gameObject);
                }
            }
        }

        Debug.Log("カメラに映すプレイヤーの数");
    }
    //レイを飛ばす処理
	private void HandleObstacleFadeForPlayers()
	{
		if (_players == null || _players.Count == 0)
			return;

		// 今フレームで「レイに当たった」フェーダー
		var hitsThisFrame = new HashSet<CameraObstacleFader>();

		Vector3 camPos = transform.position;

		// 1人 or 2人どっちでもOK：今いるプレイヤー全員にレイを飛ばす
		for (int i = 0; i < _players.Count; i++)
		{
			var player = _players[i];
			if (player == null) continue;

			Vector3 to = player.transform.position;
			Vector3 dir = to - camPos;
			float dist = dir.magnitude;
			if (dist <= 0.01f) continue;

			dir /= dist;

			int hitCount = Physics.RaycastNonAlloc(
				camPos,
				dir,
				_obstacleHits,
				dist,
				_obstacleMask,
				QueryTriggerInteraction.Ignore
			);

			for (int h = 0; h < hitCount; h++)
			{
				var hit = _obstacleHits[h];
				if (hit.collider == null) continue;

				var fader = hit.collider.GetComponentInParent<CameraObstacleFader>();
				if (fader == null) continue;

				hitsThisFrame.Add(fader);

				// まだフェードリストに入ってないなら、フェード開始
				if (!_fadingNow.Contains(fader))
				{
					fader.SetFaded(true);
					_fadingNow.Add(fader);
				}
			}
		}

		// 先フレームまでフェードしてたけど、
		// 今フレームはどのプレイヤーとの線上にもいないやつは元に戻す
		for (int i = _fadingNow.Count - 1; i >= 0; i--)
		{
			var fader = _fadingNow[i];
			if (fader == null)
			{
				_fadingNow.RemoveAt(i);
				continue;
			}

			if (!hitsThisFrame.Contains(fader))
			{
				fader.SetFaded(false);
				_fadingNow.RemoveAt(i);
			}
		}
	}

}
