using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
public enum EnemyAnimationEnum { SwordAttack, SwordSkill, StaffAttack, StaffSkill, ArrowAttack, GetAttack, Death, Reset}

public class Enemy : NetworkBehaviour
{
    public enum WeaponType
	{
		Normal, // 고블린, 어린 오크, 트롤, 성체 오크, 스켈레톤
        Hard, // 골렘, 강화 골렘 (내려찍기)
		Sword, // 검사 스켈레톤
		Staff, // 마법사 스켈레톤
		Arrow // 궁수 스켈레톤
	}

    [Header("공격 관련")]
	[SerializeField] private WeaponType currentWeapon;
	private float normalRange = 1.5f;
	private float normalAngle = 60f;
	private float hardRange = 6.0f;
	private float swordRange = 2.5f;
	private float swordAngle = 60f;
    private float staffRange = 6.0f;
    private float arrowRange = 10.0f;


    [Header("데이터 관련")]
    [SerializeField] private EnemyTableSO enemyTableSO;
    [SerializeField] private LootTableSO lootTableSO;
    [SerializeField] private ItemTableSO itemTableSO;
    [SerializeField] private GameObject droppedItem;
    public EnemyEntry EnemyEntry {get; private set;}
    [SerializeField] private int enemyID = 1000;
    [SerializeField] private float uiScaleFactor = 1f;
    [SerializeField] private Vector3 addOffset;

    [Header("컴포넌트")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator anim;
    [SerializeField] private Collider coll;

    [Header("프리팹")]
    [SerializeField] private GameObject uiNamePrefab;
    [SerializeField] private NetworkObject swordSkillPrefab;
    [SerializeField] private NetworkObject arrowProjectilePrefab;
    [SerializeField] private NetworkObject magicProjectilePrefab;

    public bool IsInitialized {get; private set;} = false;
    public bool IsDead {get; private set;} = false;

    public Action OnDeath;

    private Transform _target;

    // 이동 관련
    private Vector3 _gravityVelocity = Vector3.zero;
    private Vector3 _finalVelocity = Vector3.zero;
    private float _gravityScale = -9.81f;

    private float maxActivityRange = 20f;
    private Vector3 _spawnPosition;
    [SerializeField] private bool _isReturning = false;

    // 배회 관련
    private float wanderRadius = 5f;
    private float wanderInterval = 4f;
    private float _nextWanderTime = 0f;

    // 이동 애니메이션 동기화용
    public NetworkVariable<float> MoveX = new NetworkVariable<float>(0);
    public NetworkVariable<float> MoveZ = new NetworkVariable<float>(0);

    // 공격 관련
    private float lastAttackTime = -999f;
	private float lastSkillTime = -999f;
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
		switch (currentWeapon)
		{
			case WeaponType.Normal:
				attackRange = normalRange;
				attackAngle = normalAngle;
				break;
            case WeaponType.Hard:
				attackRange = hardRange;
				attackAngle = 0;
				break;
			case WeaponType.Sword:
				attackRange = swordRange;
				attackAngle = swordAngle;
				break;
            case WeaponType.Staff:
                attackRange = staffRange;
                attackAngle = 0;
                break;
            case WeaponType.Arrow:
                attackRange = arrowRange;
                attackAngle = 0;
                break;
		}
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

        HandleAIState();
        
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

        float distanceFromSpawn = Vector3.Distance(transform.position, _spawnPosition);

        if (distanceFromSpawn > maxActivityRange) 
        { 
            _isReturning = true; 
            _target = null; 
        }
        
        if (_isReturning && distanceFromSpawn < 3f) 
        {
            _isReturning = false;
            if (agent.isOnNavMesh) agent.ResetPath();
            _nextWanderTime = Time.time + wanderInterval * Random.Range(0.8f, 1.2f);
        }

        if (_isReturning)
        {
            MoveTowards(_spawnPosition);
        }
        else if (_target == null)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                _finalVelocity = new Vector3(0, _gravityVelocity.y, 0);

                if (Time.time >= _nextWanderTime)
                {
                    float randomRadius = wanderRadius * Random.Range(0.5f, 1.0f);
                    Vector3 randomPos = GetRandomWanderPosition(randomRadius);
                    MoveTowards(randomPos);
                    _nextWanderTime = Time.time + wanderInterval * Random.Range(0.7f, 1.3f);
                }
            }
            else
            {
                MoveTowards(agent.destination);
            }
        }
        else if (_target != null && agent.isOnNavMesh)
        {
            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist <= attackRange) 
            { 
                _finalVelocity = new Vector3(0, _gravityVelocity.y, 0); 
                TryAttack(); 
            }
            else 
            {
                MoveTowards(_target.position);
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
        
        _finalVelocity = moveDir * EnemyEntry.SPD;
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
    // ------------------------------------------------------

    // 네트워크 관련 -----------------------------------------
    public override void OnNetworkSpawn()
	{
        EnemyEntry = enemyTableSO.GetById(enemyID);
		if(IsServer)
        {
            _spawnPosition = transform.position;

            enemyHealth.Init();

            agent.enabled = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
        else
        {
            agent.enabled = false;
        }

        SoundManager.Instance.PlaySFX3D("EnemySpawn", transform.position, 0.6f);

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            _uiUsername = Instantiate(uiNamePrefab, worldUI.transform).GetComponent<UI_Username>();
            _uiUsername.Init(transform, EnemyEntry.Name, true);
            _uiUsername.SetHealth(EnemyEntry.HP, EnemyEntry.HP);
            _uiUsername.SetScaleAndOffset(uiScaleFactor, addOffset);
        }

        IsInitialized = true;
	}
    // ------------------------------------------------

    // 체력 / 사망 처리 ------------------------------------------
    public void UpdateHPBar(float newVal)
    {
        _uiUsername.SetHealth(newVal, EnemyEntry.HP);
    }

    public void ProcessDeath()
    {
        IsDead = true;
        if(IsServer)
        {
            agent.enabled = false;

            List<LootEntry> lootEntry = lootTableSO.GetByLootId(EnemyEntry.LootID);
            
            foreach(var entry in lootEntry)
            {
                if(entry.DropChance > Random.value * 100)
                {
                    Vector3 spawnPos = transform.position + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f));
                    DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
                    int quantity = Random.Range(entry.MinQty, entry.MaxQty);

                    ItemEntry itemEntry = itemTableSO.GetById(entry.ItemID);

                    DroppedItemData itemData = new DroppedItemData(new ItemEntryNS(itemEntry), quantity, 0);
                    itemObject.GetComponent<NetworkObject>().Spawn();
                    itemObject.Init(itemData);
                    if(itemEntry.ItemType == "Equipment")
                        itemObject.ItemName.Value = $"{itemEntry.Name} (+0)";
                    else 
                        itemObject.ItemName.Value = $"{itemEntry.Name} ({quantity}개)";
                    itemObject.IsInitialized.Value = true;
                }
            }

            ServerHandleAnimation(EnemyAnimationEnum.Death);
            int layersToExclude = LayerMask.GetMask("Player", "StageObject", "Enemy");
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
    // ---------------------------------------------------

    // 추격/공격 대상 설정 -----------------------------------
    public void SetTarget(Transform inTransform)
    {
        if(_target == null && !_isReturning)
        {
            _target = inTransform;
            PlaySetTargetSoundRPC();
        }
    }

    public void OnTriggerStayInTargetFindArea(Collider other)
    {
        if(!IsServer) return;
        if(_target != null || _isReturning) return;

        if(other.CompareTag("Player"))
        {
            _target = other.transform;
            PlaySetTargetSoundRPC();
        }
    }

    public void OnTriggerExitInTargetFindArea(Collider other)
    {
        if(!IsServer) return;

        if(other.CompareTag("Player"))
        {
            if(_target == other.transform)
                _target = null;
        }
    }

    [Rpc(SendTo.Everyone)]
	private void PlaySetTargetSoundRPC()
    {
		SoundManager.Instance.PlaySFX3D("EnemySetTarget", transform.position, 2f);
    }
    // --------------------------------------------------------

    // 공격 처리 -------------------------------------------------
    private void TryAttack()
	{
		if(_isAttacking) return;

		if(Time.time < lastAttackTime + (1.0f / EnemyEntry.APS))
			return;
		
        if(_target.TryGetComponent<ClientManager>(out ClientManager clientManager))
        {
            if(clientManager.IsDead.Value)
            {
                _target = null;
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
			case WeaponType.Normal:
                StartCoroutine(SwordAttackRoutine());
				break;
            case WeaponType.Hard:
                StartCoroutine(SwordSkillRoutine());
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

    private IEnumerator SwordAttackRoutine()
	{
        ServerHandleAnimation(EnemyAnimationEnum.SwordAttack);
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);
		ServerHandleSwordAttack();
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator SwordSkillRoutine()
	{
		ServerHandleAnimation(EnemyAnimationEnum.SwordSkill);
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);
		ServerHandleSwordSkill();
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator ArrowAttackRoutine()
	{
        ServerHandleAnimation(EnemyAnimationEnum.ArrowAttack);
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);
        if(_target)
        {
            Vector3 dir = (_target.transform.position - transform.position).normalized + Vector3.up * 0.15f;
		    ServerHandleArrowAttack(dir);
        }
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);

		_isAttacking = false;
	}

    private IEnumerator StaffAttackRoutine()
	{
		ServerHandleAnimation(EnemyAnimationEnum.StaffAttack);
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);
        ServerHandleStaffAttack();
		yield return new WaitForSeconds(1.0f / EnemyEntry.APS * 0.5f);

		_isAttacking = false;
	}

	private void ServerHandleSwordAttack()
	{
		GameObject attackerObject = gameObject;

		Transform attackerTransform = attackerObject.transform;
		Vector3 origin = attackerTransform.position;
		Vector3 forward = attackerTransform.forward;

		PlayerHealth bestTarget = null;
		float bestDistance = float.MaxValue;

		foreach (var userCtx in ServerManager.Instance.UserContexts)
		{
			GameObject targetObject = userCtx.Value.PlayerObject;
			if (!userCtx.Value.ClientManager.IsInitialized.Value) continue;

			Vector3 targetPos = targetObject.transform.position;
			Vector3 toTarget = targetPos - origin;

			float distance = toTarget.magnitude;
			if (distance > attackRange)
				continue;

			Vector3 dirToTarget = toTarget.normalized;
			float angle = Vector3.Angle(forward, dirToTarget);

			if (angle > attackAngle * 0.5f)
				continue;

			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestTarget = targetObject.GetComponent<PlayerHealth>();
			}
		}

		if (bestTarget != null)
		{
			DamageInfo damageInfo = new DamageInfo()
			{
				damage = EnemyEntry.ATK,
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
			0,
			EnemyEntry.ATK,
			dir
		);
		projectileObj.Spawn();
	}

	private void ServerHandleArrowAttack(Vector3 dir)
	{
		GameObject attackerObject = gameObject;
		Transform attackerTransform = attackerObject.transform;
		Vector3 spawnPos = attackerTransform.position + attackerTransform.forward + Vector3.up * 1.2f;

		NetworkObject projectileObj = Instantiate(
			arrowProjectilePrefab,
			spawnPos,
			Quaternion.LookRotation(dir)
		);

		ArrowProjectile projectile = projectileObj.GetComponent<ArrowProjectile>();
		if (projectile == null) return;

		projectile.Initialize(
			0,
			EnemyEntry.ATK,
			dir
		);
		projectileObj.Spawn();
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
			0,
			EnemyEntry.ATK,
			dir
		);
		projectileObj.Spawn();
	}

    // ---------------------------------------------------------------

    // 애니메이션 처리 --------------------------------------------------
	public void ServerHandleAnimation(EnemyAnimationEnum enemyAnimationEnum)
	{
		RequestAnimationClientRpc(Enum.GetName(typeof(EnemyAnimationEnum), enemyAnimationEnum));
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
    // ----------------------------------------------------------------
}
