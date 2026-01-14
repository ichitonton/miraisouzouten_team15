using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class YajirushiToGoal : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera targetCamera;
    [SerializeField] GameObject yajirushiPrefab;
    [SerializeField] Transform goalId1;
    [SerializeField] Transform goalId2;
    [SerializeField] Transform goalId3;
    Transform goal;

    [Header("Ground Settings")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundOffset = 0.2f;

    [Header("Screen Settings")]
    [SerializeField, Range(0f, 0.49f)]
    float screenMargin = 0.08f;

    [Header("Ray Settings")]
    [SerializeField] float rayLength = 1000f;

    private GameObject yajirushiInstance;
    private bool isVisible;

    void Start()
    {
        goal = transform;
        if (targetCamera == null)
            targetCamera = Camera.main;

        yajirushiInstance = Instantiate(yajirushiPrefab);
        yajirushiInstance.SetActive(false);
        isVisible = false;
    }
    public void SetTargetGoal(ulong clientId)
    {
        //Debug.Log("YajirushiToGoal: Id" + clientId);
        clientId %= 3;
        if (clientId == 0)
            goal = goalId1;
        else if (clientId == 1)
            goal = goalId2;
        else if (clientId == 2)
            goal = goalId3;

        goal.transform.position = new Vector3(goal.transform.position.x, 0.0f, goal.transform.position.z);
    }

    void LateUpdate()
    {
        if (yajirushiInstance == null || goal == null) return;

        //Debug.Log("YajirushiToGoal: LateUpdate running" + goal.position);
        // ================================
        // á@ å¸Ç´ÅFÉJÉÅÉâ Å® ÉSÅ[Éã
        // ================================
        Vector3 dirFromCamera = goal.position - targetCamera.transform.position;
        dirFromCamera.y = 0f;

        if (dirFromCamera.sqrMagnitude < 0.001f)
        {
            SetVisible(false);
            return;
        }

        yajirushiInstance.transform.rotation =
            Quaternion.LookRotation(dirFromCamera.normalized);

        // ================================
        // áA âÊñ í[ï˚å¸
        // ================================
        Vector3 dirLocal =
            targetCamera.transform.InverseTransformDirection(dirFromCamera);

        if (dirLocal.z < 0f)
            dirLocal.z = 0.0001f;

        Vector2 dir2D = new Vector2(dirLocal.x, dirLocal.y).normalized;

        Vector2 viewportPos = new Vector2(
            0.5f + dir2D.x * 0.5f,
            0.5f + dir2D.y * 0.5f
        );

        viewportPos.x = Mathf.Clamp(viewportPos.x, screenMargin, 1f - screenMargin);
        viewportPos.y = Mathf.Clamp(viewportPos.y, screenMargin, 1f - screenMargin);

        // ================================
        // áB ínñ  Raycast
        // ================================
        Ray ray = targetCamera.ViewportPointToRay(
            new Vector3(viewportPos.x, viewportPos.y, 0f)
        );

        bool hitGround = Physics.Raycast(ray, out RaycastHit hit, rayLength, groundLayer);
        // ínñ ÉqÉbÉgå„
        if (hitGround)
        {
            Vector3 arrowPos = hit.point + hit.normal * groundOffset;
            yajirushiInstance.transform.position = arrowPos;

            // Åö å¸Ç´ÇÅuñÓàÛà íu Å® ÉSÅ[ÉãÅvÇ…Ç∑ÇÈ
            Vector3 lookDir = goal.position - arrowPos;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                yajirushiInstance.transform.rotation =
                    Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            SetVisible(true);
        }

        else
        {
            SetVisible(false);
        }
    }

    void SetVisible(bool visible)
    {
        if (isVisible == visible) return;

        isVisible = visible;
        yajirushiInstance.SetActive(visible);
    }

    public void YajirushiOnOff(bool isOn)
    {
        if (yajirushiInstance != null)
            yajirushiInstance.SetActive(isOn);
    }
}
