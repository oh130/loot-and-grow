using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class AIPlayer : NetworkBehaviour
{
    public enum WeaponType
	{
        Hand,
		Sword,
        Staff,
		Arrow
	}

    [Header("공격 관련")]
	[SerializeField] private WeaponType currentWeapon;
    private float handRange = 1.5f;
	private float handAngle = 90f;
	private float swordRange = 2.5f;
	private float swordAngle = 140f;
    private float swordHitRadius = 1.0f;
	private float swordCloseHitRange = 1.3f;
    private float staffRange = 12.0f;
    private float arrowRange = 12.0f;
    


    [Header("데이터 관련")]
    [SerializeField] private GameObject droppedItem;
    public int[] equippedItemIDs;
    public PlayerStat AIPlayerStat;
    public NetworkVariable<float> AIPlayerMaxHP = new NetworkVariable<float>(0);

    [Header("컴포넌트")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private AIPlayerHealth aiPlayerHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator anim;
    [SerializeField] private Collider coll;
    [SerializeField] private GameObject targetFindArea;
    [SerializeField] private AIPlayerEquipmentVisual aiPlayerEquipmentVisual;
    [SerializeField] private GameObject visualRoot;

    [Header("프리팹")]
    [SerializeField] private GameObject uiNamePrefab;
    [SerializeField] private NetworkObject swordSkillPrefab;
    [SerializeField] private NetworkObject arrowProjectilePrefab;
    [SerializeField] private NetworkObject magicProjectilePrefab;
	[SerializeField] private NetworkObject staffSkillPrefab;
    [SerializeField] private NetworkObject spawnEffectPrefab;
    [SerializeField] private NetworkObject swordAttackEffect;
    [SerializeField] private NetworkObject arrowReleaseEffectPrefab;
	[SerializeField] private float arrowReleaseEffectDuration = 1.0f;
    
	[SerializeField] private NetworkObject staffReleaseEffectPrefab;
	[SerializeField] private float staffReleaseEffectDuration = 1.0f;
    [SerializeField] private NetworkObject staffSkillEffectPrefab;
	[SerializeField] private float staffSkillEffectDuration = 2.0f;

    public bool IsInitialized {get; private set;} = false;
    public bool IsDead {get; private set;} = false;

    public Action OnDeath;

    [SerializeField] private Transform _target;

    // 이동 관련
    private Vector3 _gravityVelocity = Vector3.zero;
    private Vector3 _finalVelocity = Vector3.zero;
	private Vector3 _dashDirection;
    private float _gravityScale = -9.81f;
    private float _lastDashTime = -999f;
    private float _startDashCooltime = 4f;
    private float _dashCooltime = 4f;
	private float _dashDuration = 0.25f;
    private float _dashTimer = -1f;
	private float _dashSpeed = 25f;
    private bool _isDash = false;
    private int _hitCount = 0;


    // 배회 관련
    private Vector3 _spawnPosition;
    private float wanderRadius = 100f;
    private float wanderInterval = 15f;
    private float _nextWanderTime = 0f;

    // 이동 애니메이션 동기화용
    public NetworkVariable<float> MoveX = new NetworkVariable<float>(0);
    public NetworkVariable<float> MoveZ = new NetworkVariable<float>(0);

    // 공격 관련
    private float lastAttackTime = -999f;
	private float _lastSkillTime = -999f;
	private float _skillCooltimeFactor = 0f;
	private float attackRange;
	private float attackAngle;
	private bool _isAttacking = false;

    // 닉네임/체력바
    private UI_Username _uiUsername;

    // 사운드
    private float _lastStepTime;
    [SerializeField] private float stepCooldown = 0.2f;
    
    private void Awake()
	{
        _dashCooltime = _startDashCooltime;
		SetWeaponType(WeaponType.Hand);
	}

    // 이동 처리 ------------------------------------------------------
    private void FixedUpdate()
    {
        if (!IsServer || !IsInitialized) return;

        HandleGravity();

        if (IsDead)
        {
            _finalVelocity = new Vector3(0, _gravityVelocity.y, 0);
            controller.Move(_finalVelocity * Time.fixedDeltaTime);
            return;
        }

		if (_isDash)
		{
			_dashTimer -= Time.fixedDeltaTime;
            _finalVelocity = _dashDirection * _dashSpeed;

			if (_dashTimer <= 0f)
			{
				_isDash = false;
			}
		}
        else
        {
            HandleAIState();
        }

        Vector3 localMoveDir = transform.InverseTransformDirection(_finalVelocity.normalized);
        MoveX.Value = localMoveDir.x;
        MoveZ.Value = localMoveDir.z;

        controller.Move(_finalVelocity * Time.fixedDeltaTime);
    }

    private void Update()
    {
        anim.SetFloat("MoveX", MoveX.Value);
        anim.SetFloat("MoveZ", MoveZ.Value);
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && _gravityVelocity.y < 0)
        {
            _gravityVelocity.y = -2f; 
        }
        _gravityVelocity.y += _gravityScale * Time.fixedDeltaTime;
    }

    private void HandleAIState()
    {
        if (_isAttacking)
        {
            _finalVelocity = new Vector3(0, _gravityVelocity.y, 0);
            return;
        }

        if (_target == null)
        {
            if (Time.time >= _nextWanderTime)
            {
                float randomRadius = wanderRadius * Random.Range(0.5f, 1.0f);
                Vector3 randomPos = GetRandomWanderPosition(randomRadius);
                MoveTowards(randomPos);
                _nextWanderTime = Time.time + wanderInterval * Random.Range(0.7f, 1.3f);
            }
            else
            {
                MoveTowards(agent.destination);
            }

            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                _finalVelocity = new Vector3(0, _gravityVelocity.y, 0);
            }
        }
        else if (_target != null && agent.isOnNavMesh)
        {
            if(Time.time >= _lastDashTime + _dashCooltime)
            {
                TryDash();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, _target.position);
                if (dist <= attackRange)
                { 
                    _finalVelocity = new Vector3(0, _gravityVelocity.y, 0);

                    if (currentWeapon != WeaponType.Hand && Time.time >= _lastSkillTime + _skillCooltimeFactor * (1.0f / AIPlayerStat.APS))
                        TrySkill();
                    else
                        TryAttack();
                }
                else
                {
                    MoveTowards(_target.position);
                }
            }
        }

        if (_finalVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(_finalVelocity.normalized);
            MoveX.Value = localVelocity.x;
            MoveZ.Value = localVelocity.z;
        }
        else
        {
            MoveX.Value = 0f;
            MoveZ.Value = 0f;
        }
    }

    private void MoveTowards(Vector3 targetPos)
    {
        if (!agent.isOnNavMesh) return;

        agent.SetDestination(targetPos);
        Vector3 moveDir = agent.desiredVelocity.normalized;
        
        _finalVelocity = moveDir * AIPlayerStat.SPD;
        _finalVelocity.y = _gravityVelocity.y;

        if (moveDir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.fixedDeltaTime * 10f);
        }
        agent.nextPosition = transform.position;
    }

    private Vector3 GetRandomWanderPosition(float radius)
    {
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 targetPos = _spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return _spawnPosition;
    }

    private void TryDash()
	{
		if (_isDash) return;

        if(_target)
        {
            _dashDirection = 
                ((_target.transform.position - transform.position).normalized + transform.right * (Random.Range(0,2) == 0? 1 : -1)).normalized;
        }
        else
        {
		    _dashDirection = transform.forward;
        }
		_dashDirection.y = 0f;
		_dashDirection.Normalize();

        ServerHandleAnimation(PlayerAnimationEnum.Dash);

		_isDash = true;
		_dashTimer = _dashDuration;
        _lastDashTime = Time.time;
        _dashCooltime = _startDashCooltime + Random.Range(-0.5f,0.5f);
	}

    // ------------------------------------------------------

    // 네트워크 관련 -----------------------------------------
    public override void OnNetworkSpawn()
	{
		if(IsServer)
        {
            _spawnPosition = transform.position;
            List<EquippedItemResponse> equippedItems = new List<EquippedItemResponse>();
            foreach(int id in equippedItemIDs)
            {
                equippedItems.Add(new EquippedItemResponse(id));
                aiPlayerEquipmentVisual.ServerEquip(id);
            }
            
            AIPlayerStat = PlayerStat.Calculate(GameDataSO.Instance.BaseStat, GameDataSO.Instance.EquipDetail, equippedItems);
            AIPlayerMaxHP.Value = AIPlayerStat.MaxHP;
            switch(currentWeapon)
            {
                case WeaponType.Sword:
                    AIPlayerStat.APS -= 0.3f;
                    break;
                case WeaponType.Arrow:
                    AIPlayerStat.APS -= 0.45f;
                    break;
                case WeaponType.Staff:
                    AIPlayerStat.APS -= 0.45f;
                    break;
            }

            targetFindArea.SetActive(true);

            aiPlayerHealth.Init();

            agent.enabled = true;
            agent.updatePosition = false;
            agent.updateRotation = false;

            DoSpawnEffect();
        }
        else
        {
            agent.enabled = false;
        }

        StartCoroutine(DistanceCullingRoutine());
        SoundManager.Instance.PlaySFX3D("EnemySpawn", transform.position, 0.6f);

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            _uiUsername = Instantiate(uiNamePrefab, worldUI.transform).GetComponent<UI_Username>();
            _uiUsername.Init(transform, "플레이어 봇(AI)", true);
            _uiUsername.SetHealth(AIPlayerMaxHP.Value, AIPlayerMaxHP.Value);
        }

        AIPlayerMaxHP.OnValueChanged += OnMaxHpChanged;

        IsInitialized = true;
	}

    public override void OnNetworkDespawn()
    {
        AIPlayerMaxHP.OnValueChanged -= OnMaxHpChanged;
    }

    private void OnMaxHpChanged(float oldVal, float newVal)
    {
        UpdateHPBar(aiPlayerHealth.CurrentHp.Value);
    }

	private void DoSpawnEffect()
	{
		Quaternion spawnRot = Quaternion.LookRotation(transform.forward);

		NetworkObject effectObj = Instantiate(spawnEffectPrefab, transform.position, spawnRot);
		effectObj.Spawn(true);

		ServerManager.Instance.StartCoroutine(DespawnEffectAfterDelay(effectObj, 2f));
	}

	private IEnumerator DespawnEffectAfterDelay(NetworkObject effectObj, float delay)
	{
		yield return new WaitForSeconds(delay);

		if (effectObj != null && effectObj.IsSpawned)
		{
			effectObj.Despawn(true);
		}
	}

    private void SetVisualActive(bool isActive)
    {
        if (visualRoot != null) visualRoot.SetActive(isActive);
    }

    private IEnumerator DistanceCullingRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (GameManager.Instance.player == null) continue;

            float distance = Vector3.Distance(transform.position, GameManager.Instance.player.transform.position);

            if (distance > 50f)
            {
                SetVisualActive(false);
            }
            else
            {
                SetVisualActive(true);
            }
        }
    }
    // ------------------------------------------------

    // 체력 / 사망 처리 ------------------------------------------
    public void UpdateHPBar(float newVal)
    {
        if(_uiUsername) _uiUsername.SetHealth(newVal, AIPlayerMaxHP.Value);
    }

    public void ProcessDeath()
    {
        IsDead = true;
        if(IsServer)
        {
            agent.enabled = false;

            List<LootEntry> lootEntry = new List<LootEntry>();
            foreach(int id in equippedItemIDs)
            {
                lootEntry.Add(new LootEntry(id));
            }

            int targetCount = (int)Math.Ceiling(lootEntry.Count * 0.3f);
            List<LootEntry> selectedItems = lootEntry.OrderBy(item => Guid.NewGuid()).Take(targetCount).ToList();

            foreach(var entry in selectedItems)
            {
                Vector3 spawnPos = transform.position + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f));
                DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
                int quantity = Random.Range(entry.MinQty, entry.MaxQty);

                ItemEntry itemEntry = GameDataSO.Instance.ItemTable.GetById(entry.ItemID);

                DroppedItemData itemData = new DroppedItemData(new ItemEntryNS(itemEntry), quantity, 0);
                itemObject.GetComponent<NetworkObject>().Spawn();
                itemObject.Init(itemData);
                if(itemEntry.ItemType == "Equipment")
                    itemObject.ItemName.Value = $"{itemEntry.Name} (+0)";
                else 
                    itemObject.ItemName.Value = $"{itemEntry.Name} ({quantity}개)";
                itemObject.IsInitialized.Value = true;
            }

            ServerHandleAnimation(PlayerAnimationEnum.Death);
            int layersToExclude = LayerMask.GetMask("Player", "StageObject", "Enemy", "AIPlayer");
            coll.excludeLayers = layersToExclude;
            controller.excludeLayers = layersToExclude;

            OnDeath?.Invoke();
            DOVirtual.DelayedCall(5f, () =>
            {
                if(NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn();
                }
            });
        }
    }

    public void HandleHit()
    {
        _hitCount += 1;
        if(_hitCount % 2 == 0)
        {
            if(currentWeapon == WeaponType.Staff || currentWeapon == WeaponType.Arrow)
                _dashCooltime = 0.5f;
        }
    }
    // ---------------------------------------------------

    // 추격/공격 대상 설정 -----------------------------------
    public void SetTarget(Transform inTransform)
    {
        if(_target == null)
        {
            _lastDashTime = Time.time;
            _target = inTransform;
        }
    }

    public void ResetTarget()
    {
        if(_target != null)
        {
            _target = null;
            _nextWanderTime = Time.time + 3f;
        }
    }

    public void OnTriggerStayInTargetFindArea(Collider other)
    {
        if(!IsServer) return;
        if(_target != null) return;

        SetTarget(other.transform);
    }

    public void OnTriggerExitInTargetFindArea(Collider other)
    {
        if(!IsServer) return;

        if(_target == other.transform)
        {
            ResetTarget();
        }
    }
    // --------------------------------------------------------

    // 공격 처리 -------------------------------------------------
    public void SetWeaponTypeByItemId(int itemId)
	{
		if (itemId >= 3088)
		{
			SetWeaponType(WeaponType.Arrow);
		}
		else if (itemId >= 3084 && itemId <= 3087)
		{
			SetWeaponType(WeaponType.Staff);
		}
		else
		{
			SetWeaponType(WeaponType.Sword);
		}
	}

    private void SetWeaponType(WeaponType weaponType)
	{
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
	            _skillCooltimeFactor = 7f;
				break;
            case WeaponType.Staff:
                attackRange = staffRange;
                _skillCooltimeFactor = 8f;
                break;
            case WeaponType.Arrow:
                attackRange = arrowRange;
                attackAngle = 0;
                _skillCooltimeFactor = 7f;
                break;
		}
	}

    private void TryAttack()
	{
		if(_isAttacking) return;

		if(Time.time < lastAttackTime + (1.0f / AIPlayerStat.APS))
			return;
		
        if(_target.TryGetComponent<ClientManager>(out ClientManager clientManager))
        {
            if(clientManager.IsDead.Value)
            {
                ResetTarget();
                return;
            }
        }
        else if(_target.TryGetComponent<Enemy>(out Enemy enemy))
        {
            if(enemy.IsDead)
            {
                ResetTarget();
                return;
            }
        }
        else if(_target.TryGetComponent<AIPlayer>(out AIPlayer aiPlayer))
        {
            if(aiPlayer.IsDead)
            {
                ResetTarget();
                return;
            }
        }

		_isAttacking = true;

        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            transform.DORotateQuaternion(targetRotation, 0.2f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

		switch (currentWeapon)
		{
            case WeaponType.Hand:
                StartCoroutine(HandAttackRoutine());
                break;
			case WeaponType.Sword:
                StartCoroutine(SwordAttackRoutine());
				break;
			case WeaponType.Staff:
                StartCoroutine(StaffAttackRoutine());
				break;
			case WeaponType.Arrow:
                StartCoroutine(ArrowAttackRoutine());
				break;
		}

		lastAttackTime = Time.time;
	}

    private void TrySkill()
	{
        if(_isAttacking) return;
		
        if(_target.TryGetComponent<ClientManager>(out ClientManager clientManager))
        {
            if(clientManager.IsDead.Value)
            {
                ResetTarget();
                return;
            }
        }
        else if(_target.TryGetComponent<Enemy>(out Enemy enemy))
        {
            if(enemy.IsDead)
            {
                ResetTarget();
                return;
            }
        }
        else if(_target.TryGetComponent<AIPlayer>(out AIPlayer aiPlayer))
        {
            if(aiPlayer.IsDead)
            {
                ResetTarget();
                return;
            }
        }

		_isAttacking = true;
		switch (currentWeapon)
		{
			case WeaponType.Hand:
				// 맨손은 스킬 없음
				_isAttacking = false;
				break;
			case WeaponType.Sword:
				StartCoroutine(SwordSkillRoutine());
				break;
			case WeaponType.Staff:
				StartCoroutine(StaffSkillRoutine());
				break;
			case WeaponType.Arrow:
				StartCoroutine(ArrowSkillRoutine());
				break;
		}
		_lastSkillTime = Time.time;
	}

    private IEnumerator HandAttackRoutine()
	{
		ServerHandleAnimation(PlayerAnimationEnum.HandAttack);
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.8f);
		ServerHandleSwordAttack();
        yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.2f);

		_isAttacking = false;
	}

    private IEnumerator SwordAttackRoutine()
	{
        ServerHandleAnimation(PlayerAnimationEnum.SwordAttack);
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);
        ServerHandleSwordAttackEffect();
		ServerHandleSwordAttack();
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);

		_isAttacking = false;
	}

	private void ServerHandleSwordAttackEffect()
	{
		GameObject attackerObject = gameObject;
		Transform attackerTransform = attackerObject.transform;

		Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 1.2f +
			Vector3.up * 1.0f;

		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);

		NetworkObject effectPrefab = swordAttackEffect;

		NetworkObject effectObj = Instantiate(effectPrefab, spawnPos, spawnRot);
		effectObj.Spawn(true);

		ServerManager.Instance.StartCoroutine(DespawnEffectAfterDelay(effectObj, 2f));
	}

    private IEnumerator SwordSkillRoutine()
	{
		ServerHandleAnimation(PlayerAnimationEnum.SwordSkill);
		yield return new WaitForSeconds(1.0f);
		ServerHandleSwordSkill();
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator ArrowSkillRoutine()
	{
		ServerHandleArrowAttackEffect();

		for (int i = 0; i < 2; i++)
		{
			ServerHandleAnimation(PlayerAnimationEnum.ArrowAttack);
			yield return new WaitForSeconds(0.2f);
			Vector3 dir = (_target.transform.position - transform.position).normalized + Vector3.up * 0.15f;
		    ServerHandleArrowSkill(dir);
		}
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator ArrowAttackRoutine()
	{
        ServerHandleAnimation(PlayerAnimationEnum.ArrowAttack);
        yield return new WaitForSeconds(0.4f);
        if(_target)
        {
            Vector3 dir = (_target.transform.position - transform.position).normalized + Vector3.up * 0.15f;
		    ServerHandleArrowAttack(dir);
        }
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator StaffSkillRoutine()
	{
		ServerHandleAnimation(PlayerAnimationEnum.StaffSkill);

		ServerHandleStaffSkillEffect();
		yield return new WaitForSeconds(1f);
		ServerHandleStaffSkill();
        yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.75f);

		_isAttacking = false;
	}

    private IEnumerator StaffAttackRoutine()
	{
		ServerHandleAnimation(PlayerAnimationEnum.StaffAttack);
        ServerHandleStaffAttackEffect();
		yield return new WaitForSeconds(0.75f);
        ServerHandleStaffAttack();
		yield return new WaitForSeconds(1.0f / AIPlayerStat.APS * 0.5f);

		_isAttacking = false;
	}

	private void ServerHandleSwordAttack()
	{
		GameObject attackerObject = gameObject;

		Transform attackerTransform = attackerObject.transform;
		Vector3 origin = attackerTransform.position;
		Vector3 forward = attackerTransform.forward;

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
				bestTarget = health;
			}
		}

		if (bestTarget != null)
		{
			DamageInfo damageInfo = new DamageInfo()
			{
				damage = AIPlayerStat.ATK * 0.75f,
				attackerClientId = 0,
				knockbackDir = Vector3.zero,
				knockbackPower = 0f,
                isCritical = false
			};

			bestTarget.TakeDamage(damageInfo);
		}
	}

	private void ServerHandleSwordSkill()
	{
		GameObject attackerObject = gameObject;

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
			NetworkObjectId,
			AIPlayerStat.ATK * 1.2f,
			dir
		);
		projectileObj.Spawn();
	}

	private void ServerHandleStaffSkillEffect()
	{
		GameObject attackerObject = gameObject;

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

		ServerManager.Instance.StartCoroutine(DespawnEffectAfterDelay(effectObj, staffSkillEffectDuration));
	}

	private void ServerHandleStaffSkill()
	{
		GameObject attackerObject = gameObject;

		Transform attackerTransform = attackerObject.transform;
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
			NetworkObjectId,
			AIPlayerStat.ATK * 0.6f,
			dir
		);
		projectileObj.Spawn();
	}

	private void ServerHandleArrowSkill(Vector3 dir)
	{
		GameObject attackerObject = gameObject;

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
				NetworkObjectId,
				AIPlayerStat.ATK / 4f,
				dirEach[i]
			);
			projectileObj.Spawn();
		}
	}

	private void ServerHandleArrowAttack(Vector3 dir)
	{
		GameObject attackerObject = gameObject;
		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.45f;

		NetworkObject projectileObj = Instantiate(
			arrowProjectilePrefab,
			spawnPos,
			Quaternion.LookRotation(dir)
		);

		ArrowProjectile projectile = projectileObj.GetComponent<ArrowProjectile>();
		if (projectile == null) return;

		projectile.Initialize(
			NetworkObjectId,
			AIPlayerStat.ATK * 0.75f,
			dir
		);
		projectileObj.Spawn();

        ServerHandleArrowAttackEffect();
	}

    private void ServerHandleArrowAttackEffect()
    {
        GameObject attackerObject = gameObject;
		Transform attackerTransform = attackerObject.transform;
        if (arrowReleaseEffectPrefab == null) return;
        Vector3 spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 1.0f +
			Vector3.up * 1.2f;
		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);
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

	private void ServerHandleStaffAttack()
	{
		GameObject attackerObject = gameObject;

		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.2f;
		Vector3 dir = attackerTransform.forward;

		NetworkObject projectileObj = Instantiate(
			magicProjectilePrefab,
			spawnPos,
			Quaternion.LookRotation(dir)
		);

		MagicProjectile projectile = projectileObj.GetComponent<MagicProjectile>();
		if (projectile == null) return;

		projectile.Initialize(
			NetworkObjectId,
			AIPlayerStat.ATK * 0.75f,
			dir
		);
		projectileObj.Spawn();
	}

    private void ServerHandleStaffAttackEffect()
    {
        GameObject attackerObject = gameObject;

		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos;

        if (staffReleaseEffectPrefab == null) return;
        spawnPos =
			attackerTransform.position +
			attackerTransform.forward * 0.8f +
			Vector3.up * 1.0f;
		Quaternion spawnRot = Quaternion.LookRotation(attackerTransform.forward);
		NetworkObject effectObj = Instantiate(staffReleaseEffectPrefab, spawnPos, spawnRot);
		effectObj.Spawn(true);

		ServerManager.Instance.StartCoroutine(DespawnEffectAfterDelay(effectObj, staffReleaseEffectDuration));
    }

    // ---------------------------------------------------------------

    // 애니메이션 처리 --------------------------------------------------
	public void ServerHandleAnimation(PlayerAnimationEnum animationEnum)
	{
		RequestAnimationClientRpc(Enum.GetName(typeof(PlayerAnimationEnum), animationEnum));
	}

	[Rpc(SendTo.Everyone)]
	private void RequestAnimationClientRpc(FixedString64Bytes triggerName)
	{
		AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
		string name = triggerName.ToString();

		if (!stateInfo.IsName(name))
		{
			anim.SetTrigger(name);
		}
		else
		{
			anim.Play(name, 0, 0f);
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

	public void OnSkillEvent(string type)
	{
		string sfx = "Skill";
		SoundManager.Instance.PlaySFX3D(type + sfx, transform.position, 2f);
	}

	public void OnDeathEvent(int idx)
	{
		string sfx = "Die";
		SoundManager.Instance.PlaySFX3D($"{idx}" + sfx, transform.position);
	}

	public void OnDashEvent()
	{
		SoundManager.Instance.PlaySFX3D("Dash", transform.position);
	}

	public void OnGatherEvent()
	{
		int idx = Random.Range(1,4);
		SoundManager.Instance.PlaySFX3D("Gather" + $"{idx}", transform.position);
	}
    // ----------------------------------------------------------------
}
