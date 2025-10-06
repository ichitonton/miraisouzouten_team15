using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class CameraController : MonoBehaviour
{

    private GameObject _player1;
    private GameObject _player2;

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
    }
    // Update is called once per frame
    private void LateUpdate()
    {
        //プレイヤーを登録
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        //二人いないなら
        if (players.Length < 2)
        {
            //Debug.LogError("プレイヤー二人いねーよ");
            Debug.Log("プレイヤー二人いないから繋げれねーってばよ");
            return;
        }

        _player1 = players[0];
        _player2 = players[1];

        //キー入力でピッチ角を変更
        if (Input.GetKey(KeyCode.UpArrow))
            _pitch += _rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.DownArrow))
            _pitch -= _rotateSpeed * Time.deltaTime;

        // クランプ（角度制限、真上から真横までぐらい）
        _pitch = Mathf.Clamp(_pitch, -30f, 30f);

        Debug.Log(_pitch);

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

        // 狭め（ズームイン許可判定）
        float innerBorder = 0.30f;
        bool wellInsideP1 = (viewPos1.x >= innerBorder && viewPos1.x <= 1f - innerBorder &&
                             viewPos1.y >= innerBorder && viewPos1.y <= 1f - innerBorder);
        bool wellInsideP2 = (viewPos2.x >= innerBorder && viewPos2.x <= 1f - innerBorder &&
                             viewPos2.y >= innerBorder && viewPos2.y <= 1f - innerBorder);

        //ヒステリシス判定
        if (outsideP1 || outsideP2)
        {
            // 外に出たらズームアウト
            //_wasClipped = true;
            targetZoom = targetZoom + _zoomView;
            Debug.Log("見切れ → ズームアウト補正");
        }
        else if (wellInsideP1 && wellInsideP2)
        {
            // 内側に戻ったらズームイン許可
            //_wasClipped = false;
            Debug.Log("ズームイン");
        }

        // スムーズに補間
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetZoom, Time.deltaTime * _zoomSpeed);



        //見る位置
        transform.LookAt(center);

    }
}
