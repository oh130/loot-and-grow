using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

public class PlayerRelativeCam : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private Transform cameraTarget;
	[SerializeField] private Transform playerVisual;
	[SerializeField] private VariableJoystick joy;
	[SerializeField] private GameObject minimapIndicatorArrow;

	[Header("Camera Rotation")]
	[SerializeField] private float touchSensitivity = 0.2f;
	[SerializeField] private float mouseSensitivity = 1000f;
	[SerializeField] private float minPitch = -30f;
	[SerializeField] private float maxPitch = 60f;

	[Header("Player Rotation")]
	[SerializeField] private float playerRotateSpeed = 12f;
	[SerializeField] private bool rotatePlayerOnlyWhenMove = true;

	[Header("Combat Camera Offset")]
	[SerializeField] private float combatCameraRightOffset = 0.6f;
	[SerializeField] private float combatCameraUpOffset = 0.05f;
	[SerializeField] private float combatCameraForwardOffset = 1.2f;
	[SerializeField] private float combatCameraLerpSpeed = 8f;
	[SerializeField] private float combatYawOffset = -10f;


	[Header("Camera Zoom")]
	[SerializeField] private CinemachineCamera virtualCamera;
	[SerializeField] private float zoomSensitivity = 0.05f;
	[SerializeField] private float minFOV = 35f;
	[SerializeField] private float maxFOV = 70f;

	private bool isCombatCameraActive = false;
	private Vector3 defaultCameraTargetLocalPos;
	private Vector3 desiredCameraTargetLocalPos;
	private Coroutine combatCameraRoutine;

	private float yaw;
	private float pitch;

	private int cameraTouchFingerId = -1;
	private bool isMouseCameraDragging = false;

	private void Awake()
	{
		if (virtualCamera == null)
		{
			virtualCamera = FindFirstObjectByType<CinemachineCamera>();
		}

		if (joy == null)
		{
			joy = FindFirstObjectByType<VariableJoystick>();
		}
	}

	private void Start()
	{
		Vector3 angles = cameraTarget.eulerAngles;
		yaw = angles.y + 90f;
		pitch = angles.x;

		defaultCameraTargetLocalPos = cameraTarget.localPosition;
		desiredCameraTargetLocalPos = defaultCameraTargetLocalPos;
	}

	private void Update()
	{
#if UNITY_EDITOR || UNITY_STANDALONE
		HandleMouseZoom();
#else
		HandlePinchZoom();
#endif

		RotateCameraTarget();
		RotatePlayerToCamera();
	}

	private void LateUpdate()
	{
		cameraTarget.localPosition = Vector3.Lerp(
			cameraTarget.localPosition,
			desiredCameraTargetLocalPos,
			Time.deltaTime * combatCameraLerpSpeed
		);
	}

	private bool IsTouchOverBlockedArea(Touch touch)
	{
		if (EventSystem.current == null) return false;

		if (IsPointerOverUIObject(touch.position))
			return true;

		if (joy != null)
		{
			RectTransform joyRect = joy.GetComponent<RectTransform>();

			if (joyRect != null &&
				RectTransformUtility.RectangleContainsScreenPoint(
					joyRect,
					touch.position,
					null
				))
			{
				return true;
			}
		}

		return false;
	}

	private void HandlePinchZoom()
	{
		if (virtualCamera == null || joy == null) return;
		if(!(joy.Horizontal <= 0.01f && joy.Horizontal >= -0.01f)) return;
		if(!(joy.Vertical <= 0.01f && joy.Vertical >= -0.01f)) return;

		List<Touch> nonUITouches = new List<Touch>();

		for (int i = 0; i < Input.touchCount; i++)
		{
			Touch touch = Input.GetTouch(i);

			if (!IsTouchOverBlockedArea(touch))
			{
				nonUITouches.Add(touch);
			}
		}
		if (nonUITouches.Count >= 2)
		{
			cameraTouchFingerId = -1;
			Touch t0 = nonUITouches[0];
			Touch t1 = nonUITouches[1];

			Vector2 prev0 = t0.position - t0.deltaPosition;
			Vector2 prev1 = t1.position - t1.deltaPosition;

			float prevDistance = Vector2.Distance(prev0, prev1);
			float currentDistance = Vector2.Distance(t0.position, t1.position);

			float delta = currentDistance - prevDistance;

			virtualCamera.Lens.FieldOfView -= delta * zoomSensitivity;
			virtualCamera.Lens.FieldOfView = Mathf.Clamp(
				virtualCamera.Lens.FieldOfView,
				minFOV,
				maxFOV
			);
		}
	}

	private void HandleMouseZoom()
	{
		if (virtualCamera == null) return;

		float scroll = Input.mouseScrollDelta.y;
		if (Mathf.Abs(scroll) < 0.01f) return;

		virtualCamera.Lens.FieldOfView -= scroll * 5f;
		virtualCamera.Lens.FieldOfView = Mathf.Clamp(
			virtualCamera.Lens.FieldOfView,
			minFOV,
			maxFOV
		);
	}

	public void KeepCombatCameraOffset(float duration = 3f)
	{
		if (combatCameraRoutine != null)
			StopCoroutine(combatCameraRoutine);

		combatCameraRoutine = StartCoroutine(CombatCameraOffsetRoutine(duration));
	}

	private IEnumerator CombatCameraOffsetRoutine(float duration)
	{
		isCombatCameraActive = true;
		desiredCameraTargetLocalPos =
			defaultCameraTargetLocalPos
			+ Vector3.right * combatCameraRightOffset
			+ Vector3.up * combatCameraUpOffset
			+ Vector3.forward * combatCameraForwardOffset;

		yield return new WaitForSeconds(duration);

		isCombatCameraActive = false;
		desiredCameraTargetLocalPos = defaultCameraTargetLocalPos;
		combatCameraRoutine = null;
	}

	private void RotateCameraTarget()
	{
#if UNITY_EDITOR || UNITY_STANDALONE
		HandleMouseCameraRotation();
#else
		HandleTouchCameraRotation();
#endif
		float finalYaw = yaw;
		if (isCombatCameraActive)
		{
			finalYaw += combatYawOffset;
		}

		cameraTarget.rotation = Quaternion.Euler(pitch, finalYaw, 0f);
		if (minimapIndicatorArrow.activeSelf) minimapIndicatorArrow.transform.rotation = Quaternion.Euler(0f, finalYaw, 0f);
	}

	private void HandleMouseCameraRotation()
	{
		if (Input.GetMouseButtonDown(0))
		{
			if (EventSystem.current != null &&
			EventSystem.current.IsPointerOverGameObject())
			{
				isMouseCameraDragging = false;
				return;
			}

			if (Input.mousePosition.x > Screen.width * 0.5f)
			{
				isMouseCameraDragging = true;
			}
		}

		if (Input.GetMouseButton(0) && isMouseCameraDragging)
		{
			float mouseX = Input.GetAxis("Mouse X");
			float mouseY = Input.GetAxis("Mouse Y");

			yaw += mouseX * mouseSensitivity * Time.deltaTime;
			pitch -= mouseY * mouseSensitivity * Time.deltaTime;
			pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
		}

		if (Input.GetMouseButtonUp(0))
		{
			isMouseCameraDragging = false;
		}
	}

	private void HandleTouchCameraRotation()
	{
		List<Touch> nonUITouches = new List<Touch>();
		for (int i = 0; i < Input.touchCount; i++)
		{
			Touch touch = Input.GetTouch(i);
			if (!IsTouchOverBlockedArea(touch))
			{
				nonUITouches.Add(touch);
			}
		}
		if(nonUITouches.Count >= 2 && 
		(joy.Horizontal <= 0.01f && joy.Horizontal >= -0.01f) &&
		(joy.Vertical <= 0.01f && joy.Vertical >= -0.01f)
		) return;

		if (cameraTouchFingerId == -1)
		{
			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch touch = Input.GetTouch(i);
				if ((touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) &&
					touch.position.x > Screen.width * 0.5f)
				{
					if (IsTouchOverBlockedArea(touch)) continue;
					cameraTouchFingerId = touch.fingerId;
					break;
				}
			}
		}

		if (cameraTouchFingerId != -1)
		{
			bool fingerFound = false;

			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch touch = Input.GetTouch(i);

				if (touch.fingerId != cameraTouchFingerId)
					continue;

				fingerFound = true;

				if (touch.phase == TouchPhase.Moved)
				{
					Vector2 delta = touch.deltaPosition;

					yaw += delta.x * touchSensitivity;
					pitch -= delta.y * touchSensitivity;
					pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
				}
				else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
				{
					cameraTouchFingerId = -1;
				}
				return;
			}
			if (!fingerFound)
			{
				cameraTouchFingerId = -1;
			}
		}
	}

	private void RotatePlayerToCamera()
	{
		bool hasMoveInput =
			Mathf.Abs(joy.Horizontal) > 0.01f ||
			Mathf.Abs(joy.Vertical) > 0.01f;

		if (rotatePlayerOnlyWhenMove && !hasMoveInput)
			return;

		Transform target = playerVisual != null ? playerVisual : transform;

		Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
		target.rotation = Quaternion.Slerp(
			target.rotation,
			targetRotation,
			playerRotateSpeed * Time.deltaTime
		);
	}

	public void RotatePlayerToCameraWhenAct() // 공격시 카메라 방향으로 플레이어 회전 변경
	{
		Transform target = playerVisual != null ? playerVisual : transform;

		Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
		target.rotation = targetRotation;
	}

	public void InitializeSensitivity()
	{
		float multiplier = PlayerPrefs.GetFloat("Sensitivity", 1.0f);
		mouseSensitivity = 1000f * multiplier;
	}

	public void SetMouseSensitivityMultiplier(float multiplier)
	{
		mouseSensitivity = 1000f * multiplier;
	}

	public Vector3 GetCameraForward()
	{
		Vector3 forward = cameraTarget.forward;
		return forward.normalized;
	}

	private bool IsPointerOverUIObject(Vector2 touchPosition)
	{
		PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
		eventDataCurrentPosition.position = touchPosition;

		List<RaycastResult> results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
		return results.Count > 0;
	}
}
