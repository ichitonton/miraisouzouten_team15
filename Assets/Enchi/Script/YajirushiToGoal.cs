using UnityEngine;

public class YajirushiToGoal : MonoBehaviour
{
	[Header("References")]
	[SerializeField] Camera targetCamera;
	[SerializeField] GameObject yajirushiPrefab;
	[SerializeField] Transform goalId1;
	[SerializeField] Transform goalId2;
	[SerializeField] Transform goalId3;

	[Header("Ground Settings")]
	[SerializeField] LayerMask groundLayer;
	[SerializeField] float groundOffset = 0.2f;

	[Header("Screen Settings")]
	[SerializeField, Range(0f, 0.49f)]
	float screenMargin = 0.08f;

	[Header("Ray Settings")]
	[SerializeField] float rayLength = 1000f;

	GameObject yajirushiInstance;
	bool isVisible;

	// ★ scene開始時に保存するゴール座標
	Vector3 goalPos1;
	Vector3 goalPos2;
	Vector3 goalPos3;

	// ★ 現在採用しているゴール座標
	Vector3 currentGoalPos;

	void Start()
	{
		if (targetCamera == null)
			targetCamera = Camera.main;

		// --- ゴール初期座標を保存（Transformはいじらない） ---
		goalPos1 = goalId1.position;
		goalPos2 = goalId2.position;
		goalPos3 = goalId3.position;

		// デフォルト
		currentGoalPos = goalPos1;

		yajirushiInstance = Instantiate(yajirushiPrefab);
		yajirushiInstance.SetActive(false);
		isVisible = false;
	}

	public void SetTargetGoal(ulong clientId)
	{
		clientId %= 3;

		if (clientId == 0)
			currentGoalPos = goalPos1;
		else if (clientId == 1)
			currentGoalPos = goalPos2;
		else
			currentGoalPos = goalPos3;
	}

	void LateUpdate()
	{
		if (yajirushiInstance == null) return;

		// ================================
		// ① 向き：カメラ → ゴール
		// ================================
		Vector3 dirFromCamera = currentGoalPos - targetCamera.transform.position;
		dirFromCamera.y = 0f;

		if (dirFromCamera.sqrMagnitude < 0.001f)
		{
			SetVisible(false);
			return;
		}

		// ================================
		// ② 画面端方向
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
		// ③ 地面 Raycast
		// ================================
		Ray ray = targetCamera.ViewportPointToRay(
			new Vector3(viewportPos.x, viewportPos.y, 0f)
		);

		if (Physics.Raycast(ray, out RaycastHit hit, rayLength, groundLayer))
		{
			Vector3 arrowPos = hit.point + hit.normal * groundOffset;
			yajirushiInstance.transform.position = arrowPos;

			Vector3 lookDir = currentGoalPos - arrowPos;
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
