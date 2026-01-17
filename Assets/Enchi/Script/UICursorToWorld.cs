using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static MovePlayerKey;
using static MovePlayerKeyLocal;

public class UICursorToWorld : MonoBehaviour
{
    private RectTransform uiIcon;   // UIアイコン
    private Canvas canvas;          // Canvas
    private Camera uiCamera;        // Canvas用カメラ
    private Transform worldTarget;  // 反映先3Dオブジェクト
    private Vector3 _lastValidTargetPos;
    private bool _hasLastValidPos = false; 
    private Vector2 _prevScreenPos;
    Vector2 center;   // プレイヤー位置（スクリーン座標）
    float radius;    // 操作可能半径


    [SerializeField] private LayerMask groundLayer;  // 地面レイヤー
    [SerializeField] private GameObject itemTarget;
    [SerializeField] private float maxDistance = 12f; // 最大距離

    public Vector3 CurrentTargetPos { get; private set; }

    Gamepad gamepad;


    Vector2 screenPos;
    void Start()
    {
        //if (worldTarget != null)
        //{
        //    SpawnTarget();
        //}
    }

    public void SpawnTarget()
    {        
        canvas = Object.FindFirstObjectByType<InGameUIController>().gameObject.GetComponent<Canvas>();//キャンバスは1つにしないとちゃんと取得できない
        uiCamera = Camera.main;
        worldTarget =Instantiate(itemTarget, transform.position, Quaternion.identity).transform;

        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            if (canvas.transform.GetChild(i).GetComponent<UICursor>() != null)
            {
                uiIcon = canvas.transform.GetChild(i).GetComponent<RectTransform>();
                if(!uiIcon.GetComponent<UICursor>().GetIsConnectPlayer())
                    {
                    uiIcon.GetComponent<UICursor>().SetIsConnectPlayer(true);
                    break;
                }
            }
        }
        screenPos.x = Screen.width / 2;
        screenPos.y = Screen.height / 2;

        //Debug.Log(canvas);
        //Debug.Log(uiCamera);
        //Debug.Log(worldTarget);
    }
    void Update()
    {   // ====== 必須：null チェック ======
        if (canvas == null || uiCamera == null || uiIcon == null || worldTarget == null)
        {
            Debug.Log("canvas:" + canvas + "uiCamera" + uiCamera + "uiIcon" + uiIcon  + "worldTarget" + worldTarget);
            return; // 必要な準備ができてないので処理しない
        }
        var pads = Gamepad.all;

        for (int i = 0; i < pads.Count; i++)
        {
            Gamepad pad = pads[i];
            //if (pad.buttonSouth.wasPressedThisFrame)
            //{
            //    Debug.Log($"Player {i + 1} : A button pressed!");
            //}
        }

        int playerNum = (int)GetComponent<MovePlayerKey>().GetPlayerNumber();

        if (pads.Count >= playerNum)
        {
            if (pads[playerNum - 1] != null)
            {
                gamepad = pads[playerNum - 1];

                Vector2 move = gamepad.rightStick.ReadValue(); 
                
                Vector2 input = move * 10f;
                Vector2 next = screenPos + input;

                // X方向の制限
                if (next.x < 0f || next.x > Screen.width)
                {
                    input.x = 0f; // はみ出す成分だけ無効化
                }

                // Y方向の制限
                if (next.y < 0f || next.y > Screen.height)
                {
                    input.y = 0f;
                }

                // 最終適用
                screenPos += input;

                // 念のためClamp（保険）
                screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
                screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);

            }
        }
        else
        {
            // Canvas が Overlay か ScreenSpace-Camera かで処理を分ける
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenPos = RectTransformUtility.WorldToScreenPoint(null, uiIcon.position);
            }
            else
            {
                screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, uiIcon.position);
            }
        }

        // 2. スクリーン座標を元にレイを飛ばす
        Ray ray = uiCamera.ScreenPointToRay(screenPos);
        if (Physics.SphereCast(ray, 0.1f, out RaycastHit hit, 1000f, groundLayer))
        {
            Vector3 targetPos = hit.point + hit.normal * 0.1f;

            Vector3 offset = targetPos - transform.position;

            if (offset.magnitude > maxDistance)
            {
                offset = offset.normalized * maxDistance;
                targetPos = transform.position + offset;
                float minY = hit.collider.bounds.max.y + 0.05f;
                targetPos.y = Mathf.Max(targetPos.y, minY);
            }

            worldTarget.position = targetPos;
            CurrentTargetPos = targetPos;

            // ★保存
            _lastValidTargetPos = targetPos;
            _hasLastValidPos = true;
        }
        else
        {
            if (_hasLastValidPos)
            {
                worldTarget.position = _lastValidTargetPos;
                CurrentTargetPos = _lastValidTargetPos;
            }
        }

    }

    public Transform GetItemTargetTransform()
    {
        return worldTarget;
    }
}
