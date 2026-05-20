using Unity.Netcode;
using UnityEngine;

public class ArrowProjectile : NetworkBehaviour
{
	[SerializeField] private bool isEnemyAttack = false;
	[SerializeField] private float speed = 15f;
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private float lifeTime = 2f;

	private ulong attackerClientId;
	private float damage;
	private Vector3 direction;
	private bool initialized = false;
	private Vector3 velocity;

	public void Initialize(ulong attackerId, float atk, Vector3 dir)
	{
		attackerClientId = attackerId;
		damage = atk;
		direction = dir.normalized;
		velocity = direction * speed;
		initialized = true;
	}

	public void SetSpeed(float value)
	{
		speed = value;
		velocity = direction * speed;
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

		velocity += Vector3.down * gravity * Time.deltaTime;
		transform.position += velocity * Time.deltaTime;

		if (velocity.sqrMagnitude > 0.001f)
			transform.rotation = Quaternion.LookRotation(velocity.normalized);
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!IsServer || !initialized) return;

		if(!isEnemyAttack)
		{
			PlayerHealth targetPlayer = other.GetComponent<PlayerHealth>();
			EnemyHealth targetEnemy = other.GetComponent<EnemyHealth>();

			if (targetPlayer == null && targetEnemy == null) return;
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

			if (targetPlayer != null)
			{
				targetPlayer.TakeDamage(damageInfo);
			}
			else if (targetEnemy != null)
			{
				targetEnemy.TakeDamage(damageInfo);
			}

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}
		else
		{
			PlayerHealth targetPlayer = other.GetComponent<PlayerHealth>();

			if (targetPlayer == null) return;

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = attackerClientId,
				knockbackDir = direction,
				knockbackPower = 0f,
				isCritical = false
			};

			if (targetPlayer != null)
			{
				targetPlayer.TakeDamage(damageInfo);
			}

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}
	}
}