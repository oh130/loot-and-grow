using Unity.Netcode;
using UnityEngine;

public class MagicProjectile : NetworkBehaviour
{
	[SerializeField] private bool isEnemyAttack = false;
	[SerializeField] private bool isAIPlayerAttack = false;
	[SerializeField] private float speed = 10f;
	[SerializeField] private float lifeTime = 2f;

	private ulong attackerClientId;
	private float damage;
	private Vector3 direction;
	private bool initialized = false;

	public void Initialize(ulong attackerId, float atk, Vector3 dir)
	{
		attackerClientId = attackerId;
		damage = atk;
		direction = dir.normalized;
		initialized = true;
	}

	public override void OnNetworkSpawn()
	{
		if (IsServer)
		{
			DestroyAfterTime();
		}
	}

	private async void DestroyAfterTime()
	{
		await Awaitable.WaitForSecondsAsync(lifeTime);
		if (this != null && NetworkObject != null && NetworkObject.IsSpawned)
			NetworkObject.Despawn();
	}

	private void Update()
	{
		if (!IsServer || !initialized) return;
		transform.position += direction * speed * Time.deltaTime;
		transform.Rotate(Vector3.forward * 720f * Time.deltaTime);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!IsServer || !initialized) return;

		if(isEnemyAttack)
		{
			IHealth targetHealth = other.GetComponent<IHealth>();
			EnemyHealth targetEnemy = other.GetComponent<EnemyHealth>();

			if (targetHealth == null || targetEnemy != null) return; // 에너미끼리는 안 때리도록 검사

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = 0,
				knockbackDir = direction,
				knockbackPower = 0f,
				isCritical = false
			};

			targetHealth?.TakeDamage(damageInfo);

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}
		else if(isAIPlayerAttack)
		{
			IHealth targetHealth = other.GetComponent<IHealth>();
			AIPlayerHealth targetAIPlayer = other.GetComponent<AIPlayerHealth>();
			if(targetHealth == null) return;
			if(targetAIPlayer != null && targetAIPlayer.NetworkObjectId == attackerClientId) return;

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = 0,
				knockbackDir = direction,
				knockbackPower = 0f,
				isCritical = false
			};

			targetHealth?.TakeDamage(damageInfo);

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}
		else
		{
			IHealth targetHealth = other.GetComponent<IHealth>();
			PlayerHealth targetPlayer = other.GetComponent<PlayerHealth>();

			if (targetHealth == null) return;
			if (targetPlayer != null && targetPlayer.OwnerClientId == attackerClientId) return;

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = attackerClientId,
				knockbackDir = direction,
				knockbackPower = 0f,
				isCritical = false
			};

            if(Random.Range(0, 1f) < ServerManager.Instance.UserContexts[attackerClientId].PlayerRuntimeStat.CHC.Value)
			{
                damageInfo.damage = damageInfo.damage * 1.5f;
				damageInfo.isCritical = true;
			}

			targetHealth?.TakeDamage(damageInfo);

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}
	}        
}