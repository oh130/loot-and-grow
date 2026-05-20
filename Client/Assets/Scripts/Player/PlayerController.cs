using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    // [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravityScale = -9.81f;
	[SerializeField] private float dashSpeed = 25f;
	[SerializeField] private CharacterController controller;
	[SerializeField] private Transform cameraTarget;
	[SerializeField] private VariableJoystick joy;
	[SerializeField] private PlayerAnimSync animSync;
	[SerializeField] private PlayerCombat playerCombat;
	[SerializeField] private ClientManager clientManager;
	[SerializeField] private PlayerRuntimeStat playerRuntimeStat;

	private Vector3 velocity = Vector3.zero;
    private bool _canMove = true;
    private bool _isDash = false;
	private float dashTimer = 0f;
	private float dashDuration = 0.25f;
	private float dashCooldown = 1.3f;
	private float dashCooldownTimer = 0f;
	private Vector3 dashDirection;

	private void FixedUpdate()
    {
        if (!IsOwner || !_canMove) return; // 이거 지우지 말아주세요! 지우면 캐릭터 물리 연산이 정상적으로 안 됩니다.

		if (!clientManager.IsInitialized.Value) return; // 초기 설정 완료되었는지 확인

		if (playerCombat != null)
		{
			bool shouldBlockMove =
				playerCombat.IsAttacking &&
				(
					playerCombat.CurrentWeapon == PlayerCombat.WeaponType.Hand ||
					playerCombat.CurrentWeapon == PlayerCombat.WeaponType.Sword
				);

			if (shouldBlockMove)
			{
				return;
			}
		}

		if (_isDash)
		{
			return;
		}

		if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = 0f;
        }
		velocity.y += gravityScale * Time.deltaTime;
		controller.Move(velocity * Time.deltaTime);

		float inputX = joy.Horizontal;
		float inputZ = joy.Vertical;

		Vector3 forward = cameraTarget.forward;
		Vector3 right = cameraTarget.right;

		forward.y = 0f;
		right.y = 0f;

		forward.Normalize();
		right.Normalize();
		Vector3 move = forward * inputZ + right * inputX;

		float moveSpeedMultiplier = 1f;

		if (playerCombat != null && playerCombat.IsChargingStaff)
		{
			moveSpeedMultiplier = playerCombat.StaffChargeMoveSpeedMultiplier;
		}

		controller.Move(
			move *
			Time.deltaTime *
			playerRuntimeStat.SPD.Value *
			moveSpeedMultiplier
		);
	}

	private void Update()
	{
		if (!IsOwner) return;

		if (!clientManager.IsInitialized.Value) return; // 초기 설정 완료되었는지 확인

		if (playerCombat != null && playerCombat.IsCastingStaff)
		{
			return;
		}

		if (dashCooldownTimer > 0f)
		{
			dashCooldownTimer -= Time.deltaTime;
		}
		if (_isDash)
		{
			dashTimer -= Time.deltaTime;

			controller.Move(
				dashDirection *
				dashSpeed *
				Time.deltaTime
			);

			if (dashTimer <= 0f)
			{
				_isDash = false;
			}
		}
	}

	public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Debug.Log("[PlayerController] 내 캐릭터가 생성되었습니다!");

            CinemachineCamera cineCam = GameManager.Instance.cineCam;
            cineCam.Target = new Unity.Cinemachine.CameraTarget();
			cineCam.Target.TrackingTarget = cameraTarget;
			cineCam.Target.LookAtTarget = cameraTarget;
			joy = FindFirstObjectByType<VariableJoystick>();
		}
    }

    public void SetMovement(bool flag)
    {
        controller.enabled = flag;
        _canMove = flag;
        if(!flag) velocity = Vector3.zero;
    }

	public void OnDashButtonPressed()
	{
		if (!IsOwner) return;
		TryDash();
	}

	private void TryDash()
	{
		if (_isDash || !_canMove) return;
		if (dashCooldownTimer > 0f) return;
		if (!clientManager.IsInitialized.Value) return;

		if (playerCombat != null && playerCombat.IsCastingStaff)
		{
			return;
		}

		dashDirection = transform.forward.normalized;

		_isDash = true;
		dashTimer = dashDuration;
		dashCooldownTimer = dashCooldown;

		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Dash);
	}

	// public void ApplyStat(PlayerStat stat)
	// {
	//     moveSpeed = stat.SPD;
	// }
}