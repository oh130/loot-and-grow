using Unity.Netcode;
using UnityEngine;

public class MagicProjectile : NetworkBehaviour
{
	[SerializeField] private bool isEnemyAttack = false;
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

		if(!isEnemyAttack)
		{
			PlayerHealth pHealth = other.GetComponent<PlayerHealth>();
			EnemyHealth eHealth = other.GetComponent<EnemyHealth>();

			if (pHealth == null && eHealth == null) return;
			if (pHealth != null && pHealth.OwnerClientId == attackerClientId) return;

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

			if (pHealth != null)
			{
				pHealth.TakeDamage(damageInfo);
			}
			else if (eHealth != null)
			{
				eHealth.TakeDamage(damageInfo);
			}

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
			}
		else
		{
			PlayerHealth pHealth = other.GetComponent<PlayerHealth>();

			if (pHealth == null) return;
			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = attackerClientId,
				knockbackDir = direction,
				knockbackPower = 0f,
				isCritical = false
			};

			if (pHealth != null)
			{
				pHealth.TakeDamage(damageInfo);
			}

			if (NetworkObject != null && NetworkObject.IsSpawned)
				NetworkObject.Despawn();
		}        
	}
}