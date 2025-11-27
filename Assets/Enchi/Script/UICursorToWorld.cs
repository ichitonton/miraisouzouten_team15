using UnityEngine;

public class UICursorToWorld : MonoBehaviour
{
    private RectTransform uiIcon;   // UIアイコン
    private Canvas canvas;          // Canvas
    private Camera uiCamera;        // Canvas用カメラ
    private Transform worldTarget;  // 反映先3Dオブジェクト
    [SerializeField] private LayerMask groundLayer;  // 地面レイヤー
    [SerializeField] private GameObject itemTarget;

    void Start()
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
    }

    void Update()
    {
        Vector2 screenPos;

        // Canvas が Overlay か ScreenSpace-Camera かで処理を分ける
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPos = RectTransformUtility.WorldToScreenPoint(null, uiIcon.position);
        }
        else
        {
            screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, uiIcon.position);
        }

        // 2. スクリーン座標を元にレイを飛ばす
        Ray ray = uiCamera.ScreenPointToRay(screenPos);

        // 3. レイキャスト（地面レイヤーのみ）
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            //Debug.Log("hit: " + hit.point);

            worldTarget.position = hit.point;
        }
    }

    public Transform GetItemTargetTransform()
    {
        return worldTarget;
    }
}
