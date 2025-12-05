using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using static MovePlayerKey;

public class UICursorToWorld : MonoBehaviour
{
    private RectTransform uiIcon;   // UIアイコン
    private Canvas canvas;          // Canvas
    private Camera uiCamera;        // Canvas用カメラ
    private Transform worldTarget;  // 反映先3Dオブジェクト
    [SerializeField] private LayerMask groundLayer;  // 地面レイヤー
    [SerializeField] private GameObject itemTarget;


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
        canvas = Object.FindFirstObjectByType<Canvas>();
        uiCamera =  Object.FindFirstObjectByType<Camera>();
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
        screenPos.x = Screen.height / 2;
    }

    void Update()
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
        gamepad = pads[(int)GetComponent<MovePlayerKey>().GetPlayerNumber() - 1];

        Vector2 move = gamepad.rightStick.ReadValue();

        screenPos += move * 10f;

        //// Canvas が Overlay か ScreenSpace-Camera かで処理を分ける
        //if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        //{
        //    screenPos = RectTransformUtility.WorldToScreenPoint(null, uiIcon.position);
        //}
        //else
        //{
        //    screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, uiIcon.position);
        //}

        // 2. スクリーン座標を元にレイを飛ばす
        Ray ray = uiCamera.ScreenPointToRay(screenPos);

        // 3. レイキャスト（地面レイヤーのみ）
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Debug.Log("hit: " + hit.point);

            worldTarget.position = hit.point + Vector3.up * 0.01f;
        }
    }

    public Transform GetItemTargetTransform()
    {
        return worldTarget;
    }
}
