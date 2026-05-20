using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;
public enum PlayerAnimationEnum { Neutral, Jump, Dash, HandAttack, SwordAttack, SwordAttack2, SwordAttack3, SwordSkill, StaffCharge, StaffAttack, StaffSkill, ArrowCharge, ArrowAttack, GetAttack, Death, Reset, Gather, Open}

public class PlayerAnimSync : NetworkBehaviour
{
	[SerializeField] private Animator animator;
	[SerializeField] private VariableJoystick joy;
	[SerializeField] private ClientManager clientManager;

	private NetworkVariable<float> netMoveX = new(
		0f,
		NetworkVariableReadPermission.Everyone,
		NetworkVariableWritePermission.Owner
	);

	private NetworkVariable<float> netMoveZ = new(
		0f,
		NetworkVariableReadPermission.Everyone,
		NetworkVariableWritePermission.Owner
	);

	private float _lastStepTime;
    [SerializeField] private float stepCooldown = 0.2f;

	private void Awake()
	{
		if (joy == null)
		{
			joy = FindFirstObjectByType<VariableJoystick>();
		}
	}

	private void Update()
	{
		if (IsOwner)
		{
			if (!clientManager.IsInitialized.Value) return; // 초기 설정 완료되었는지 확인

			float inputX = joy.Horizontal;
			float inputZ = joy.Vertical;

			netMoveX.Value = inputX;
			netMoveZ.Value = inputZ;

			animator.SetFloat("MoveX", inputX);
			animator.SetFloat("MoveZ", inputZ);
		}
		else
		{
			animator.SetFloat("MoveX", netMoveX.Value);
			animator.SetFloat("MoveZ", netMoveZ.Value);
		}
	}

	[ServerRpc]
	public void RequestAnimationServerRpc(PlayerAnimationEnum playerAnimationEnum) // 애니메이션 요청 서버 RPC 하나로 합쳐놨습니다. 이 스크립트 맨위에 이넘 추가하시면 됩니다.
	{
		RequestAnimationClientRpc(Enum.GetName(typeof(PlayerAnimationEnum), playerAnimationEnum));
	}

	[Rpc(SendTo.Everyone)]
	private void RequestAnimationClientRpc(FixedString64Bytes triggerName)
	{
		AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
		string name = triggerName.ToString();

		if (!stateInfo.IsName(name))
		{
			animator.SetTrigger(name);
		}
		else
		{
			animator.Play(name, 0, 0f);
		}
	}

	public void SetAnimation(string triggerName)
	{
		AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
		string name = triggerName;

		if (!stateInfo.IsName(name))
		{
			animator.SetTrigger(name);
		}
		else
		{
			animator.Play(name, 0, 0f);
		}
	}

	// 애니메이션 - 사운드 동기화 -------------------------------------------------
	public void OnWalkEvent()
	{
		if (Time.time - _lastStepTime < stepCooldown) return;
		_lastStepTime = Time.time;

		SoundManager.Instance.PlaySFX3D("Walk", transform.position);
	}

	public void OnAttackEvent(string type)
	{
		string sfx = "Attack";
		SoundManager.Instance.PlaySFX3D(type + sfx, transform.position);
		// switch(type)
		// {
		// 	case "Hand":
		// 		break;
		// 	case "Sword":
		// 		break;
		// 	case "Staff":
		// 		break;
		// 	case "Arrow":
		// 		break;
		// }
	}

	public void OnDashEvent()
	{
		SoundManager.Instance.PlaySFX3D("Dash", transform.position);
	}

	public void OnSkillEvent(string type)
	{
		string sfx = "Skill";
		SoundManager.Instance.PlaySFX3D(type + sfx, transform.position, 1.5f);
	}

	public void OnDeathEvent(int idx)
	{
		string sfx = "Die";
		SoundManager.Instance.PlaySFX3D($"{idx}" + sfx, transform.position);
	}

	public void OnGatherEvent()
	{
		int idx = Random.Range(1,4);
		SoundManager.Instance.PlaySFX3D("Gather" + $"{idx}", transform.position);
	}
}