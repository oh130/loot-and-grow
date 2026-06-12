using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;

public class DamageInfo
{
	public float damage;
	public ulong attackerClientId;
	public Vector3 knockbackDir; // 넉백 미구현
	public float knockbackPower;
	public bool isCritical; 
}

public class PlayerCombat : NetworkBehaviour
{
	public enum WeaponType
	{
		Hand,
		Sword,
		Staff,
		Arrow
	}
	[SerializeField] private WeaponType currentWeapon;

	[Header("Attack - Hand")]
	[SerializeField] private float handRange = 2.0f;
	[SerializeField] private float handAngle = 90f;

	[Header("Attack - Sword")]
	[SerializeField] private float swordRange = 2.8f;
	[SerializeField] private float swordAngle = 140f;
	[SerializeField] private float swordHitRadius = 1.0f;
	[SerializeField] private float swordCloseHitRange = 1.3f;
	[SerializeField] private float comboResetTime = 1.0f;
	[SerializeField] private NetworkObject swordSkillPrefab;

	[Header("Sword Attack Effects")]
	[SerializeField] private NetworkObject swordAttackEffect1;
	[SerializeField] private NetworkObject swordAttackEffect2;
	[SerializeField] private NetworkObject swordAttackEffect3;

	[Header("Attack - Staff")]
	[SerializeField] private NetworkObject magicProjectilePrefab;
	[SerializeField] private Transform projectileSpawnPoint;
	[SerializeField] private NetworkObject staffSkillPrefab;
	[SerializeField] private float minStaffChargeTime = 1.2f;
	[SerializeField] private float staffChargeMoveSpeedMultiplier = 0.4f;

	[SerializeField] private float staffAutoTargetRange = 35f;
	[SerializeField] private float staffAutoTargetAngle = 80f;
	[SerializeField] private float staffAimHeightOffset = 1.0f;
	[SerializeField] private LayerMask staffTargetMask;
	[SerializeField] private GameObject staffTargetIndicatorPrefab;

	[Header("Staff Skill Effects")]
	[SerializeField] private NetworkObject staffSkillEffectPrefab;
	[SerializeField] private float staffSkillEffectDuration = 2.0f;

	[Header("Staff Attack Effects")]
	[SerializeField] private NetworkObject staffChargeEffectPrefab;
	[SerializeField] private NetworkObject staffReleaseEffectPrefab;
	[SerializeField] private float staffReleaseEffectDuration = 1.0f;
	[SerializeField] private NetworkObject staffChargeSubEffectPrefab;

	[Header("Attack - Arrow")]
	[SerializeField] private float minChargeTime = 0.2f;
	[SerializeField] private float maxChargeTime = 0.6f;
	[SerializeField] private float minArrowDamageMultiplier = 0.5f;
	[SerializeField] private float maxArrowDamageMultiplier = 1.5f;
	[SerializeField] private float minArrowSpeed = 10f;
	[SerializeField] private float maxArrowSpeed = 25f;
	[SerializeField] private float arrowAutoTargetRange = 60f;
	[SerializeField] private float arrowAimHeightOffset = 1.0f;
	[SerializeField] private float arrowAutoTargetRadius = 4.0f;
	[SerializeField] private float arrowAutoTargetAngle = 70f;
	[SerializeField] private NetworkObject arrowProjectilePrefab;
	[SerializeField] private Transform arrowSpawnPoint;
	[SerializeField] private LineRenderer arrowTrajectoryLine;
	[SerializeField] private GameObject targetIndicatorPrefab;
	[SerializeField] private LayerMask arrowTargetMask;

	[Header("Arrow Attack Effects")]
	[SerializeField] private NetworkObject arrowReleaseEffectPrefab;
	[SerializeField] private float arrowReleaseEffectDuration = 1.0f;

	[Header("References")]
	[SerializeField] private Transform attackOrigin;
	[SerializeField] private Animator animator;
	[SerializeField] private PlayerAnimSync animSync;
	[SerializeField] private ClientManager clientManager;
	[SerializeField] private PlayerRuntimeStat playerRuntimeStat;
	[SerializeField] private PlayerRelativeCam playerRelativeCam;

	private float lastAttackTime = -999f;
	private float lastSkillTime = -999f;
	private float attackRange;
	private float attackAngle;
	private int comboStep = 0;
	private float lastComboTime = 0f;
	private bool isChargingArrow = false;
	private float arrowChargeStartTime = 0f;
	private int trajectoryPointCount = 30;
	private float trajectoryTimeStep = 0.08f;
	private float trajectoryHitRadius = 0.25f;
	private GameObject currentTargetIndicator;
	private GameObject currentStaffTargetIndicator;
	private NetworkObject currentStaffChargeEffect;
	private NetworkObject currentStaffChargeSubEffect;
	private GameObject skillButton;

	private bool _isAttacking = false; // 공격과 스킬을 동시 사용 못하도록
	public bool IsAttacking => _isAttacking;
	public WeaponType CurrentWeapon => currentWeapon;
	private float _skillCooltimeFactor = 0f;

	private bool isChargingStaff = false;
	private float staffChargeStartTime = 0f;
	public bool IsChargingStaff => isChargingStaff;
	public float StaffChargeMoveSpeedMultiplier => staffChargeMoveSpeedMultiplier;
	private bool isCastingStaff = false;
	public bool IsCastingStaff => isCastingStaff; // 스태프 공격 판단 변수, 플레이어 이동 스크립트에서 참조용 // 이동 불가하게 해야하는 경우에 공용으로 쓰겠습니다!

	private bool isCastingArrow = false;

	public Action<float> OnAttack; // 공격시 발생하는 이벤트. UI_HUD에서 받아서 버튼 쿨타임 표시
	public Action<float> OnSkill; // 스킬 사용시 발생하는 이벤트

	public void SetWeaponTypeByItemId(int itemId)
	{
		if (itemId == -1)
		{
			SetWeaponType(WeaponType.Hand);
			skillButton.SetActive(false);
			return;
		}
		if (itemId >= 3088)
		{
			SetWeaponType(WeaponType.Arrow);
			skillButton.SetActive(true);
		}
		else if (itemId >= 3084 && itemId <= 3087)
		{
			SetWeaponType(WeaponType.Staff);
			skillButton.SetActive(true);
		} // d
		else
		{
			SetWeaponType(WeaponType.Sword);
			skillButton.SetActive(true);
		}
	}

	private void SetWeaponType(WeaponType weaponType)
	{
		ForceCancelArrowCharge();
		if (animSync != null)
		{
			animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Neutral);
		}

		currentWeapon = weaponType;

		switch (currentWeapon)
		{
			case WeaponType.Hand:
				attackRange = handRange;
				attackAngle = handAngle;
				break;

			case WeaponType.Sword:
				attackRange = swordRange;
				attackAngle = swordAngle;
				break;
		}
	}

	private void Awake()
	{
		skillButton = GameObject.Find("Button_Skill");
		switch (currentWeapon)
		{
			case WeaponType.Hand:
				attackRange = handRange;
				attackAngle = handAngle;
				break;
			case WeaponType.Sword:
				attackRange = swordRange;
				attackAngle = swordAngle;
				// attackDamage = swordDamage; // 공격 데미지, 쿨타임은 PlayerRuntimeStat에서 관리하는 NetworkVarible을 기반으로 작동하게 함 (변조 방지)
				// attackCooldown = swordCooldown;
				break;

			case WeaponType.Staff:
				//attackRange = staffRange;
				//attackAngle = staffAngle;
				// attackDamage = staffDamage;
				// attackCooldown = staffCooldown;
				break;
		}
	}

	private void Update()
	{
		if (!IsOwner) return;

		if (isChargingArrow)
		{
			RefreshCombatCamera();
			UpdateArrowTrajectoryPreview();
		}
		if (isChargingStaff)
		{
			RefreshCombatCamera();
			UpdateStaffTargetPreview();
		}
	}

	private void RefreshCombatCamera()
	{
		if (playerRelativeCam != null)
		{
			playerRelativeCam.KeepCombatCameraOffset(3f);
		}
	}

	public void OnAttackButtonDown()
	{
		if (!IsOwner) return;

		if (currentWeapon == WeaponType.Arrow)
		{
			StartArrowCharge();
		}
		else if (currentWeapon == WeaponType.Staff)
		{
			StartStaffCharge();
		}
		else
		{
			TryAttack();
		}
	}

	public void OnAttackButtonUp()
	{
		if (!IsOwner) return;

		if (currentWeapon == WeaponType.Arrow)
		{
			ReleaseArrowCharge();
		}
		else if (currentWeapon == WeaponType.Staff)
		{
			ReleaseStaffCharge();
		}
	}

	public void OnSkillButtonPressed()
	{
		if (!IsOwner) return;
		TrySkill();
	}

	private void TryAttack()
	{
		if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료되었는지 확인
		if(clientManager.IsDead.Value) return;

		if (_isAttacking || isCastingStaff || isCastingArrow) return;

		if (Time.time < lastAttackTime + (1.0f / playerRuntimeStat.APS.Value)) // clientManager.IsInitialized.Value == true이면 런타임 능력치가 계산되어 반영된 상태
			return;
		
		_isAttacking = true;

		playerRelativeCam.RotatePlayerToCameraWhenAct();

		if (currentWeapon != WeaponType.Hand && currentWeapon != WeaponType.Sword)
		{
			RefreshCombatCamera();
		}

		switch (currentWeapon)
		{
			case WeaponType.Hand:
				StartCoroutine(HandAttackRoutine());
				break;
			case WeaponType.Sword:
				HandleSwordCombo();
				break;
			//case WeaponType.Staff:
			//	// if (isCastingStaff) return; // 앞부분으로 이동
			//	StartCoroutine(StaffAttackRoutine());
			//	break;
			//case WeaponType.Arrow:
			//	//StartCoroutine(ArrowAttackRoutine());
			//	break;
		}
		lastAttackTime = Time.time;

		OnAttack?.Invoke(1.0f / playerRuntimeStat.APS.Value);
	}

	private void TrySkill()
	{
		if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료되었는지 확인
		if(clientManager.IsDead.Value) return;

		if(_isAttacking || isCastingStaff || isCastingArrow) return;

		if (Time.time < lastSkillTime + _skillCooltimeFactor * (1.0f / playerRuntimeStat.APS.Value)) // clientManager.IsInitialized.Value == true이면 런타임 능력치가 계산되어 반영된 상태
			return;
		
		_isAttacking = true;

		playerRelativeCam.RotatePlayerToCameraWhenAct();
		RefreshCombatCamera();
		switch (currentWeapon)
		{
			case WeaponType.Hand:
				// 맨손은 스킬 없음
				_isAttacking = false;
				break;
			case WeaponType.Sword:
				StartCoroutine(SwordSkillRoutine());
				_skillCooltimeFactor = 7.0f;
				break;
			case WeaponType.Staff:
				StartCoroutine(StaffSkillRoutine());
				_skillCooltimeFactor = 8.0f;
				break;
			case WeaponType.Arrow:
				StartCoroutine(ArrowSkillRoutine());
				_skillCooltimeFactor = 7.0f;
				break;
		}
		lastSkillTime = Time.time;
		
		OnSkill?.Invoke(_skillCooltimeFactor * (1.0f / playerRuntimeStat.APS.Value));
	}

	private void HandleSwordCombo()
	{
		float attackCooldown = 1.0f / playerRuntimeStat.APS.Value;
		if (Time.time - lastComboTime > attackCooldown + comboResetTime)
			comboStep = 0;

		comboStep++;
		lastComboTime = Time.time;
		_isAttacking = true;

		switch (comboStep)
		{
			case 1:
				animSync.RequestAnimationServerRpc(PlayerAnimationEnum.SwordAttack);
				StartCoroutine(SwordAttackRoutine(1));
				//Debug.Log("combo 1");
				break;

			case 2:
				animSync.RequestAnimationServerRpc(PlayerAnimationEnum.SwordAttack2);
				StartCoroutine(SwordAttackRoutine(2));
				//Debug.Log("combo 2");
				break;

			case 3:
				animSync.RequestAnimationServerRpc(PlayerAnimationEnum.SwordAttack3);
				StartCoroutine(SwordAttackRoutine(3));
				comboStep = 0;
				//Debug.Log("combo 3");
				break;
		}
		lastAttackTime = Time.time;
	}

	private IEnumerator HandAttackRoutine()
	{
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.HandAttack);
		yield return new WaitForSeconds(0.8f);
		RequestSwordAttackServerRpc(1);
		_isAttacking = false;
	}

	private IEnumerator SwordAttackRoutine(int comboIndex)
	{
		yield return new WaitForSeconds(0.6f);
		if (comboIndex == 3) yield return new WaitForSeconds(0.3f);

		RequestSwordAttackEffectServerRpc(comboIndex);
		RequestSwordAttackServerRpc(comboIndex);

		yield return new WaitForSeconds(0.3f);

		_isAttacking = false;
	}

	private IEnumerator SwordSkillRoutine()
	{
		isCastingStaff = true;
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.SwordSkill);

		yield return new WaitForSeconds(1f);
		RequestSwordSkillServerRpc();

		_isAttacking = false;
		isCastingStaff = false;
	}

	// 애니메이션 이후 발사체 생성
	private IEnumerator StaffAttackRoutine()
	{
		isCastingStaff = true;
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.StaffAttack);

		yield return new WaitForSeconds(1f);
		Vector3 dir = playerRelativeCam.GetCameraForward().normalized;
		RequestStaffAttackServerRpc(dir);

		_isAttacking = false;
		isCastingStaff = false;
	}

	[ServerRpc]
	private void RequestStaffSkillEffectServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos = attackerTransform.position;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		if (staffSkillEffectPrefab == null) return;

		NetworkObject effectObj = Instantiate(
			staffSkillEffectPrefab,
			spawnPos,
			spawnRot
		);

		effectObj.Spawn(true);

		StartCoroutine(DespawnEffectAfterDelay(effectObj, staffSkillEffectDuration));
	}

	private IEnumerator StaffSkillRoutine()
	{
		isCastingStaff = true;
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.StaffSkill);

		RequestStaffSkillEffectServerRpc();

		yield return new WaitForSeconds(1f);
		RequestStaffSkillServerRpc();
		yield return new WaitForSeconds(1.25f);
		// animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Reset);

		_isAttacking = false;
		isCastingStaff = false;
	}

	private bool IsTargetReachableByArrowTrajectory(
	Vector3 startPos,
	Vector3 dir,
	float arrowSpeed,
	Transform target)
	{
		Vector3 velocity = dir * arrowSpeed;
		Vector3 targetPos = target.position + Vector3.up * arrowAimHeightOffset;

		for (int i = 0; i < trajectoryPointCount; i++)
		{
			float t = i * trajectoryTimeStep;

			Vector3 point =
				startPos +
				velocity * t +
				0.5f * Physics.gravity * t * t;

			float distance = Vector3.Distance(point, targetPos);

			if (distance <= trajectoryHitRadius * 1.5f)
				return true;
		}

		return false;
	}

	[ServerRpc]
	private void RequestArrowReleaseEffectServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 1.0f +
			Vector3.up * 1.2f;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		if (arrowReleaseEffectPrefab == null) return;

		NetworkObject effectObj = Instantiate(
			arrowReleaseEffectPrefab,
			spawnPos,
			spawnRot
		);

		effectObj.Spawn(true);

		StartCoroutine(
			DespawnEffectAfterDelay(
				effectObj,
				arrowReleaseEffectDuration
			)
		);
	}

	private bool TryCalculateBallisticDirection(Vector3 origin, Vector3 targetPos, float speed,	out Vector3 fireDir)
	{
		fireDir = Vector3.zero;

		Vector3 toTarget = targetPos - origin;
		Vector3 toTargetXZ = Vector3.ProjectOnPlane(toTarget, Vector3.up);

		float x = toTargetXZ.magnitude;
		float y = toTarget.y;
		float g = Mathf.Abs(Physics.gravity.y);
		float v2 = speed * speed;
		float v4 = v2 * v2;

		float discriminant = v4 - g * (g * x * x + 2f * y * v2);

		if (discriminant < 0f)
		{
			return false;
		}

		float sqrt = Mathf.Sqrt(discriminant);
		float angle = Mathf.Atan((v2 - sqrt) / (g * x));

		Vector3 dirXZ = toTargetXZ.normalized;

		fireDir =
			dirXZ * Mathf.Cos(angle) +
			Vector3.up * Mathf.Sin(angle);

		fireDir.Normalize();

		return true;
	}

	private bool TryGetArrowAutoTarget(Vector3 origin, Vector3 aimDir, out Transform target, out Vector3 targetDir)
	{
		target = null;
		targetDir = aimDir;

		Collider[] hits = Physics.OverlapSphere(
			origin,
			arrowAutoTargetRange,
			arrowTargetMask
		);

		float bestScore = float.MaxValue;

		float chargeTime = Time.time - arrowChargeStartTime;
		float chargeRatio = Mathf.Clamp01(chargeTime / maxChargeTime);
		float arrowSpeed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, chargeRatio);

		Vector3 velocity = aimDir * arrowSpeed;

		foreach (Collider hit in hits)
		{
			if (hit.transform.root == transform.root) continue;

			Vector3 targetPos = hit.transform.position + Vector3.up * arrowAimHeightOffset;

			float distanceFromOrigin = Vector3.Distance(origin, targetPos);
			if (distanceFromOrigin > arrowAutoTargetRange) continue;

			float lastT = (trajectoryPointCount - 1) * trajectoryTimeStep;

			Vector3 lastPoint =
				origin +
				velocity * lastT +
				0.5f * Physics.gravity * lastT * lastT;

			float trajectoryReachDistance = Vector3.Distance(
				Vector3.ProjectOnPlane(origin, Vector3.up),
				Vector3.ProjectOnPlane(lastPoint, Vector3.up)
			);

			float targetFlatDistance = Vector3.Distance(
				Vector3.ProjectOnPlane(origin, Vector3.up),
				Vector3.ProjectOnPlane(targetPos, Vector3.up)
			);

			if (targetFlatDistance > trajectoryReachDistance)
				continue;

			Vector3 toTarget = targetPos - origin;
			Vector3 dirToTarget = toTarget.normalized;

			float angle = Vector3.Angle(aimDir, dirToTarget);

			if (angle > arrowAutoTargetAngle * 0.5f)
				continue;

			float closestDistance = float.MaxValue;

			for (int i = 0; i < trajectoryPointCount; i++)
			{
				float t = i * trajectoryTimeStep;

				Vector3 point =
					origin +
					velocity * t +
					0.5f * Physics.gravity * t * t;

				float colliderRadius = hit.bounds.extents.magnitude * 0.7f;
				float distance = Mathf.Max(0f, Vector3.Distance(point, targetPos) - colliderRadius);

				if (distance < closestDistance)
					closestDistance = distance;
			}

			if (closestDistance > arrowAutoTargetRadius)
				continue;
			
			if (closestDistance < bestScore)
			{
				bestScore = closestDistance;
				target = hit.transform;
			}
		}

		if (target == null) return false;


		Vector3 finalTargetPos =target.position + Vector3.up * arrowAimHeightOffset;

		if (!TryCalculateBallisticDirection(
			origin,
			finalTargetPos,
			arrowSpeed,
			out targetDir))
		{
			targetDir = (finalTargetPos - origin).normalized;
		}

		return true;
	}

	private bool TryGetArrowAutoTargetDirection(Vector3 origin, Vector3 aimDir, out Vector3 targetDir)
	{
		return TryGetArrowAutoTarget(origin, aimDir, out _, out targetDir);
	}

	private void StartArrowCharge()
	{
		if (!clientManager.IsInitialized.Value) return;
		if (clientManager.IsDead.Value) return;
		if (_isAttacking || isCastingStaff || isCastingArrow) return;

		float attackCooldown = 1.0f / playerRuntimeStat.APS.Value;

		if (Time.time < lastAttackTime + attackCooldown)
			return;

		RefreshCombatCamera();

		isChargingArrow = true;
		isCastingArrow = true;
		_isAttacking = true;

		arrowChargeStartTime = Time.time;
		playerRelativeCam.RotatePlayerToCameraWhenAct();
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.ArrowCharge);

		if (arrowTrajectoryLine != null) arrowTrajectoryLine.enabled = true;
	}

	private void ReleaseArrowCharge()
	{
		if (!isChargingArrow) return;

		float chargeTime = Time.time - arrowChargeStartTime;

		if (chargeTime < minChargeTime)
		{
			isChargingArrow = false;
			isCastingArrow = false;
			_isAttacking = false;

			animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Neutral);
			HideArrowTrajectoryPreview();

			return;
		}

		float chargeRatio = Mathf.Clamp01(chargeTime / maxChargeTime);

		float damageMultiplier = Mathf.Lerp(
			minArrowDamageMultiplier,
			maxArrowDamageMultiplier,
			chargeRatio
		);

		float arrowSpeed = Mathf.Lerp(
			minArrowSpeed,
			maxArrowSpeed,
			chargeRatio
		);

		Vector3 origin = transform.position + transform.forward + Vector3.up * 1.2f;
		Vector3 rawDir = (playerRelativeCam.GetCameraForward() + Vector3.up * 0.2f).normalized;
		Vector3 dir = rawDir;
		TryGetArrowAutoTargetDirection(origin, rawDir, out dir);

		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.ArrowAttack);
		RequestArrowReleaseEffectServerRpc();

		RequestChargedArrowAttackServerRpc(dir, damageMultiplier, arrowSpeed);

		isChargingArrow = false;
		isCastingArrow = false;
		_isAttacking = false;

		lastAttackTime = Time.time; // 발사 이후부터 쿨타임 시작

		OnAttack?.Invoke(1.0f / playerRuntimeStat.APS.Value);
		HideArrowTrajectoryPreview();
		RefreshCombatCamera();
	}

	private void UpdateArrowTrajectoryPreview()
	{
		if (arrowTrajectoryLine == null) return;

		float chargeTime = Time.time - arrowChargeStartTime;
		float chargeRatio = Mathf.Clamp01(chargeTime / maxChargeTime);

		float arrowSpeed = Mathf.Lerp(
			minArrowSpeed,
			maxArrowSpeed,
			chargeRatio
		);

		Vector3 startPos = transform.position + transform.forward + Vector3.up * 1.2f;
		Vector3 rawDir = (playerRelativeCam.GetCameraForward() + Vector3.up * 0.2f).normalized;

		Transform autoTarget;
		Vector3 dir;

		bool hasAutoTarget = TryGetArrowAutoTarget(
			startPos,
			rawDir,
			out autoTarget,
			out dir
		);

		Vector3 velocity = dir * arrowSpeed;

		arrowTrajectoryLine.positionCount = trajectoryPointCount;

		bool foundTarget = false;
		RaycastHit bestHit = default;

		Vector3 prevPoint = startPos;

		for (int i = 0; i < trajectoryPointCount; i++)
		{
			float t = i * trajectoryTimeStep;

			Vector3 point =
				startPos +
				velocity * t +
				0.5f * Physics.gravity * t * t;

			arrowTrajectoryLine.SetPosition(i, point);

			if (i > 0 && !foundTarget)
			{
				Vector3 segment = point - prevPoint;
				float distance = segment.magnitude;

				if (Physics.SphereCast(
					prevPoint,
					trajectoryHitRadius,
					segment.normalized,
					out RaycastHit hit,
					distance,
					arrowTargetMask
				))
				{
					foundTarget = true;
					bestHit = hit;
				}
			}

			prevPoint = point;
		}

		if (hasAutoTarget)
		{
			ShowTargetIndicator(autoTarget.position);
		}
		else
		{
			HideTargetIndicator();
		}
	}

	public void ForceCancelArrowCharge()
	{
		if (!isChargingArrow && !isCastingArrow) return;

		isChargingArrow = false;
		isCastingArrow = false;
		_isAttacking = false;

		HideArrowTrajectoryPreview();
	}

	private void ShowTargetIndicator(Vector3 position)
	{
		if (targetIndicatorPrefab == null) return;

		if (currentTargetIndicator == null)
		{
			currentTargetIndicator = Instantiate(targetIndicatorPrefab);
		}

		currentTargetIndicator.SetActive(true);
		currentTargetIndicator.transform.position = position + Vector3.up * 0.05f;
		currentTargetIndicator.transform.rotation = Quaternion.identity;
	}

	private void HideArrowTrajectoryPreview()
	{
		if (arrowTrajectoryLine != null)
		{
			arrowTrajectoryLine.enabled = false;
			arrowTrajectoryLine.positionCount = 0;
		}

		HideTargetIndicator();
	}

	private void HideTargetIndicator()
	{
		if (currentTargetIndicator != null)
		{
			currentTargetIndicator.SetActive(false);
		}
	}

	[ServerRpc]
	public void ForceCancelStaffChargeServerRpc()
	{
		isChargingStaff = false;
		isCastingStaff = false;

		if (currentStaffChargeEffect != null && currentStaffChargeEffect.IsSpawned)
		{
			currentStaffChargeEffect.Despawn(true);
			currentStaffChargeEffect = null;
		}

		if (currentStaffChargeSubEffect != null && currentStaffChargeSubEffect.IsSpawned)
		{
			currentStaffChargeSubEffect.Despawn(true);
			currentStaffChargeSubEffect = null;
		}
	}

	private bool TryGetStaffAutoTarget(
	Vector3 origin,
	Vector3 aimDir,
	out Transform target,
	out Vector3 targetDir)
	{
		target = null;
		targetDir = aimDir;

		Collider[] hits = Physics.OverlapSphere(origin, staffAutoTargetRange, staffTargetMask);

		float bestScore = float.MaxValue;

		foreach (Collider hit in hits)
		{
			if (hit.gameObject == gameObject) continue;

			Vector3 targetPos = hit.transform.position + Vector3.up * staffAimHeightOffset;
			Vector3 toTarget = targetPos - origin;

			float distance = toTarget.magnitude;
			if (distance > staffAutoTargetRange) continue;

			Vector3 dirToTarget = toTarget.normalized;
			float angle = Vector3.Angle(aimDir, dirToTarget);

			if (angle > staffAutoTargetAngle * 0.5f) continue;

			float score = angle * 0.7f + distance * 0.3f;

			if (score < bestScore)
			{
				bestScore = score;
				target = hit.transform;
				targetDir = dirToTarget;
			}
		}

		return target != null;
	}

	private void UpdateStaffTargetPreview()
	{
		Vector3 origin = transform.position + transform.forward + Vector3.up * 1.2f;
		Vector3 rawDir = playerRelativeCam.GetCameraForward().normalized;

		bool hasTarget = TryGetStaffAutoTarget(
			origin,
			rawDir,
			out Transform target,
			out Vector3 dir
		);

		if (hasTarget)
		{
			ShowStaffTargetIndicator(target.position);
		}
		else
		{
			HideStaffTargetIndicator();
		}
	}

	private void ShowStaffTargetIndicator(Vector3 position)
	{
		if (staffTargetIndicatorPrefab == null) return;

		if (currentStaffTargetIndicator == null)
		{
			currentStaffTargetIndicator = Instantiate(staffTargetIndicatorPrefab);
		}

		currentStaffTargetIndicator.SetActive(true);
		currentStaffTargetIndicator.transform.position = position + Vector3.up * 0.05f;
		currentStaffTargetIndicator.transform.rotation = Quaternion.identity;
	}

	private void HideStaffTargetIndicator()
	{
		if (currentStaffTargetIndicator != null)
		{
			currentStaffTargetIndicator.SetActive(false);
		}
	}

	[ServerRpc]
	private void RequestStopStaffChargeEffectServerRpc(ServerRpcParams rpcParams = default)
	{
		if (currentStaffChargeEffect != null && currentStaffChargeEffect.IsSpawned)
		{
			currentStaffChargeEffect.Despawn(true);
			currentStaffChargeEffect = null;
		}

		if (currentStaffChargeSubEffect != null && currentStaffChargeSubEffect.IsSpawned)
		{
			currentStaffChargeSubEffect.Despawn(true);
			currentStaffChargeSubEffect = null;
		}
	}

	[ServerRpc]
	private void RequestStaffReleaseEffectServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 0.8f +
			Vector3.up * 1.0f;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		if (staffReleaseEffectPrefab == null) return;

		NetworkObject effectObj = Instantiate(staffReleaseEffectPrefab, spawnPos, spawnRot);
		effectObj.Spawn(true);

		StartCoroutine(DespawnEffectAfterDelay(effectObj, staffReleaseEffectDuration));
	}

	[ServerRpc]
	private void RequestStaffChargeEffectServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos =
			attackerTransform.position +
			Vector3.up * 1.0f;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		if (staffChargeEffectPrefab == null) return;

		if (currentStaffChargeEffect != null && currentStaffChargeEffect.IsSpawned)
		{
			currentStaffChargeEffect.Despawn(true);
		}

		if (currentStaffChargeSubEffect != null && currentStaffChargeSubEffect.IsSpawned)
		{
			currentStaffChargeSubEffect.Despawn(true);
		}

		//currentStaffChargeEffect = Instantiate(staffChargeEffectPrefab, spawnPos, spawnRot);
		//currentStaffChargeEffect.Spawn(true);

		//currentStaffChargeSubEffect = Instantiate(staffChargeSubEffectPrefab, spawnPos + Vector3.up * 1.5f, spawnRot);
		//currentStaffChargeSubEffect.Spawn(true);

		currentStaffChargeEffect = Instantiate(staffChargeEffectPrefab, spawnPos, spawnRot);
		currentStaffChargeEffect.Spawn(true);
		currentStaffChargeEffect.transform.SetParent(attackerTransform);
		

		currentStaffChargeSubEffect = Instantiate(staffChargeSubEffectPrefab, spawnPos + Vector3.up * 1.5f, spawnRot);
		currentStaffChargeSubEffect.Spawn(true);
		currentStaffChargeSubEffect.transform.SetParent(attackerTransform);
		
	}

	private void StartStaffCharge()
	{
		if (!clientManager.IsInitialized.Value) return;
		if (clientManager.IsDead.Value) return;
		if (_isAttacking || isCastingStaff || isCastingArrow) return;

		float attackCooldown = 1.0f / playerRuntimeStat.APS.Value;

		if (Time.time < lastAttackTime + attackCooldown)
			return;

		isChargingStaff = true;
		isCastingStaff = true;
		_isAttacking = true;

		staffChargeStartTime = Time.time;

		playerRelativeCam.RotatePlayerToCameraWhenAct();
		RefreshCombatCamera();

		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.StaffCharge);
		RequestStaffChargeEffectServerRpc();
	}

	private IEnumerator StaffFireDelayRoutine(Vector3 dir)
	{
		yield return new WaitForSeconds(0.7f);

		RequestStaffAttackServerRpc(dir);

		isCastingStaff = false;
		_isAttacking = false;

		lastAttackTime = Time.time;
		OnAttack?.Invoke(1.0f / playerRuntimeStat.APS.Value);
	}

	private void ReleaseStaffCharge()
	{
		if (!isChargingStaff) return;

		float chargeTime = Time.time - staffChargeStartTime;

		isChargingStaff = false;
		RequestStopStaffChargeEffectServerRpc();

		if (chargeTime < minStaffChargeTime)
		{
			// 덜 차징된 상태에서 손 떼면 무효
			isCastingStaff = false;
			_isAttacking = false;
			animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Neutral);
			HideStaffTargetIndicator();
			return;
		}

		// 풀차징 성공
		playerRelativeCam.RotatePlayerToCameraWhenAct();
		RefreshCombatCamera();
		animSync.RequestAnimationServerRpc(PlayerAnimationEnum.StaffAttack);
		RequestStaffReleaseEffectServerRpc();

		Vector3 origin = transform.position + transform.forward + Vector3.up * 1.2f;
		Vector3 rawDir = playerRelativeCam.GetCameraForward().normalized;
		Vector3 dir = rawDir;

		TryGetStaffAutoTarget(origin, rawDir, out _, out dir);

		StartCoroutine(StaffFireDelayRoutine(dir));
		HideStaffTargetIndicator();
	}

	//private IEnumerator ArrowAttackRoutine()
	//{
	//	isCastingArrow = true;
	//	animSync.RequestAnimationServerRpc(PlayerAnimationEnum.ArrowAttack);

	//	yield return new WaitForSeconds(0.2f);
	//	Vector3 dir = (playerRelativeCam.GetCameraForward() + Vector3.up * 0.5f).normalized;
	//	RequestArrowAttackServerRpc(dir);

	//	_isAttacking = false;
	//	isCastingArrow = false;
	//}

	private IEnumerator ArrowSkillRoutine()
	{
		isCastingArrow = true;
		RequestArrowReleaseEffectServerRpc();

		for (int i = 0; i < 2; i++)
		{
			animSync.RequestAnimationServerRpc(PlayerAnimationEnum.ArrowAttack);

			yield return new WaitForSeconds(0.2f);
			Vector3 dir = (playerRelativeCam.GetCameraForward() + Vector3.up * 0.15f).normalized;
			RequestArrowSkillServerRpc(dir);
		}

		_isAttacking = false;
		isCastingArrow = false;
	}

	// ���� ���� ���� ����
	[ServerRpc]
	private void RequestSwordAttackServerRpc(int comboIndex, ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;
		UserContext attackUserContext = null;
		if(ServerManager.Instance)
		{
			attackUserContext = ServerManager.Instance.UserContexts[attackerClientId];
		}
		else return;
		if(attackUserContext == null) return;
		GameObject attackerObject = attackUserContext.PlayerObject; // 존재 보장됨

		// ulong attackerClientId = rpcParams.Receive.SenderClientId;
		// if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(attackerClientId)) return;
		// NetworkObject attackerObject = NetworkManager.Singleton.ConnectedClients[attackerClientId].PlayerObject;
		// if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;
		Vector3 origin = attackOrigin != null ? attackOrigin.position : attackerTransform.position;
		Vector3 forward = attackerTransform.forward;

		// PlayerHealth bestTargetPlayer = null;
		// EnemyHealth bestTargetEnemy = null;
		IHealth bestTarget = null;
		float bestScore = float.MaxValue;

		int targetLayer = LayerMask.GetMask("Player", "Enemy", "AIPlayer");

		Vector3 capsuleStart = origin + Vector3.up * 0.5f;
		Vector3 capsuleEnd =
			origin +
			forward * swordRange +
			Vector3.up * 0.5f;

		Collider[] hitColliders = Physics.OverlapCapsule(
			capsuleStart,
			capsuleEnd,
			swordHitRadius,
			targetLayer
		);

		foreach (var hit in hitColliders)
		{
			if (hit.gameObject == attackerObject) continue;

			// PlayerHealth pHealth = hit.GetComponent<PlayerHealth>();
			// EnemyHealth eHealth = hit.GetComponent<EnemyHealth>();
			IHealth health = hit.GetComponent<IHealth>();

			if (health == null) continue;

			Vector3 targetPos = hit.ClosestPoint(origin);
			Vector3 toTarget = targetPos - origin;

			float distance = toTarget.magnitude;
			if (distance > swordRange + swordHitRadius) continue;

			Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
			Vector3 flatToTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized;

			float angle = Vector3.Angle(flatForward, flatToTarget);

			bool isCloseEnough = distance <= swordCloseHitRange;
			bool isInFrontArc = angle <= swordAngle * 0.5f;

			if (!isCloseEnough && !isInFrontArc) continue;

			float score = distance + angle * 0.02f;

			if (score < bestScore)
			{
				bestScore = score;
				// bestTargetPlayer = pHealth;
				// bestTargetEnemy = eHealth;
				bestTarget = health;
			}
		}

		if (bestTarget != null)
        {
			float baseDamage = playerRuntimeStat.ATK.Value;
			float comboMultiplier = comboIndex switch
			{
				1 => 1.0f,
				2 => 1.2f,
				3 => 1.5f,
				_ => 1.0f
			};
			float finalDamage = Mathf.Max(1, baseDamage * comboMultiplier);

			DamageInfo damageInfo = new DamageInfo()
            {
                damage = finalDamage,
                attackerClientId = attackerClientId,
                knockbackDir = Vector3.zero,
                knockbackPower = 0f,
				isCritical= false
            };

            if(Random.Range(0, 1f) < playerRuntimeStat.CHC.Value)
			{
                damageInfo.damage = damageInfo.damage * 1.5f;
				damageInfo.isCritical = true;
			}

			bestTarget?.TakeDamage(damageInfo);
            // if (bestTargetPlayer != null) 
            // {
            //     bestTargetPlayer.TakeDamage(damageInfo);
            //     NotifyHitClientRpc(bestTargetPlayer.OwnerClientId);
            // }
            // else if (bestTargetEnemy != null) 
            // {
            //     bestTargetEnemy.TakeDamage(damageInfo);
            // }
        }
	}

	[ServerRpc]
	private void RequestSwordAttackEffectServerRpc(int comboIndex, ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 1.2f +
			Vector3.up * 1.0f;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		NetworkObject effectPrefab = comboIndex switch
		{
			1 => swordAttackEffect1,
			2 => swordAttackEffect2,
			3 => swordAttackEffect3,
			_ => null
		};

		if (effectPrefab == null) return;

		NetworkObject effectObj = Instantiate(effectPrefab, spawnPos, spawnRot);
		effectObj.Spawn(true);

		StartCoroutine(DespawnEffectAfterDelay(effectObj, 2f));
	}

	private IEnumerator DespawnEffectAfterDelay(NetworkObject effectObj, float delay)
	{
		yield return new WaitForSeconds(delay);

		if (effectObj != null && effectObj.IsSpawned)
		{
			effectObj.Despawn(true);
		}
	}

	// 스태프 공격
	[ServerRpc]
	private void RequestStaffAttackServerRpc(Vector3 dir, ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;
		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.2f;

		Vector3 finalDir = dir.normalized;

		NetworkObject projectileObj = Instantiate(
			magicProjectilePrefab,
			spawnPos,
			Quaternion.LookRotation(finalDir)
		);

		MagicProjectile projectile = projectileObj.GetComponent<MagicProjectile>();
		if (projectile == null) return;

		projectile.Initialize(
			attackerClientId,
			playerRuntimeStat.ATK.Value,
			finalDir
		);

		projectileObj.Spawn();
	}

	// 활 공격
	[ServerRpc]
	private void RequestChargedArrowAttackServerRpc(
	Vector3 dir,
	float damageMultiplier,
	float arrowSpeed,
	ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;

		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;
		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward +
			Vector3.up * 1.2f;

		Vector3 finalDir = dir.normalized;

		NetworkObject projectileObj = Instantiate(
			arrowProjectilePrefab,
			spawnPos,
			Quaternion.LookRotation(finalDir)
		);

		ArrowProjectile projectile = projectileObj.GetComponent<ArrowProjectile>();
		if (projectile == null) return;

		float finalDamage = playerRuntimeStat.ATK.Value * damageMultiplier;

		projectile.Initialize(
			attackerClientId,
			finalDamage,
			finalDir
		);
		projectile.SetSpeed(arrowSpeed);
		projectileObj.Spawn();
	}

	[ServerRpc]
	private void RequestSwordSkillServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;
		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward;
		Vector3 dir = attackerTransform.forward;

		NetworkObject projectileObj = Instantiate(
			swordSkillPrefab,
			spawnPos,
			Quaternion.LookRotation(dir)
		);

		SwordSkill projectile = projectileObj.GetComponent<SwordSkill>();
		if (projectile == null) return;

		projectile.Initialize(
			attackerClientId,
			playerRuntimeStat.ATK.Value * 1.75f,
			dir
		);
		projectileObj.Spawn();
	}

	[ServerRpc]
	private void RequestStaffSkillServerRpc(ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;
		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;
		// Vector3 spawnPos = attackerTransform.position + attackerTransform.forward * 0.0f + Vector3.up * 2.5f + attackerTransform.right * 0.75f;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward * 1.25f + Vector3.up * 1.5f;
		Vector3 dir = attackerTransform.forward - Vector3.up * 0.05f;

		NetworkObject projectileObj = Instantiate(
			staffSkillPrefab,
			spawnPos,
			Quaternion.LookRotation(dir)
		);

		StaffSkill projectile = projectileObj.GetComponent<StaffSkill>();
		if (projectile == null) return;

		projectile.Initialize(
			attackerClientId,
			playerRuntimeStat.ATK.Value * 1f,
			dir
		);
		projectileObj.Spawn();
	}

	[ServerRpc]
	private void RequestArrowSkillServerRpc(Vector3 dir, ServerRpcParams rpcParams = default)
	{
		ulong attackerClientId = rpcParams.Receive.SenderClientId;
		if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

		GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
		if (attackerObject == null) return;

		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.2f;
		Vector3[] spawnPosEach = {spawnPos, spawnPos + attackerTransform.right * 0.2f, spawnPos + attackerTransform.right * (-0.2f), spawnPos + attackerTransform.up * 0.4f};
		Vector3[] dirEach = {dir, dir + attackerTransform.right * 0.05f, dir + attackerTransform.right * (-0.05f), dir + attackerTransform.up * 0.1f};

		for(int i = 0; i < 4; i++)
		{
			NetworkObject projectileObj = Instantiate(
				arrowProjectilePrefab,
				spawnPosEach[i],
				Quaternion.LookRotation(dirEach[i])
			);

			ArrowProjectile projectile = projectileObj.GetComponent<ArrowProjectile>();
			if (projectile == null) return;

			projectile.Initialize(
				attackerClientId,
				playerRuntimeStat.ATK.Value / 2f,
				dirEach[i]
			);
			projectileObj.Spawn();
		}
	}

	// [ClientRpc]
	// private void NotifyHitClientRpc(ulong targetClientId)
	// {
	// 	if (NetworkManager.Singleton.LocalClientId != targetClientId)
	// 		return;

	// 	Debug.Log("�ǰ� ������");
	// }
}

//[ServerRpc]
//private void RequestArrowAttackServerRpc(Vector3 dir, ServerRpcParams rpcParams = default)
//{
//	ulong attackerClientId = rpcParams.Receive.SenderClientId;
//	if (!ServerManager.Instance.UserContexts.ContainsKey(attackerClientId)) return;

//	GameObject attackerObject = ServerManager.Instance.UserContexts[attackerClientId].PlayerObject;
//	if (attackerObject == null) return;

//	Transform attackerTransform = attackerObject.transform;
//	Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.2f;

//	NetworkObject projectileObj = Instantiate(
//		arrowProjectilePrefab,
//		spawnPos,
//		Quaternion.LookRotation(dir)
//	);

//	ArrowProjectile projectile = projectileObj.GetComponent<ArrowProjectile>();
//	if (projectile == null) return;

//	projectile.Initialize(
//		attackerClientId,
//		playerRuntimeStat.ATK.Value,
//		dir
//	);
//	projectileObj.Spawn();
//}