using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class SwordSkill : NetworkBehaviour
{
	[SerializeField] private bool isEnemyAttack = false;
    [SerializeField] private ParticleSystem particle;
	private float lifeTime = 3f;

	private ulong attackerClientId;
	private float damage;
	private Vector3 direction;
	private bool initialized = false;

    private HashSet<ulong> _savedTarget = new HashSet<ulong>();

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
            transform.DOMove(transform.position + 9f * direction, 1.5f);
		}
        particle.Clear();
        particle.Play();
	}

	private async void DestroyAfterTime()
	{
		await Awaitable.WaitForSecondsAsync(lifeTime);
		if (this != null && NetworkObject != null && NetworkObject.IsSpawned)
			NetworkObject.Despawn();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!IsServer || !initialized) return;

		if(!isEnemyAttack)
		{
			if (!other.TryGetComponent(out NetworkObject targetNetObj)) return;

			PlayerHealth pHealth = other.GetComponent<PlayerHealth>();
			EnemyHealth eHealth = other.GetComponent<EnemyHealth>();

			if (pHealth == null && eHealth == null) return;
			if (pHealth != null && pHealth.OwnerClientId == attackerClientId) return;

			if (_savedTarget.Contains(targetNetObj.NetworkObjectId)) return;
			_savedTarget.Add(targetNetObj.NetworkObjectId);

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

			if (pHealth != null) pHealth.TakeDamage(damageInfo);
			else if (eHealth != null) eHealth.TakeDamage(damageInfo);
			// if (NetworkObject != null && NetworkObject.IsSpawned)
			// 	NetworkObject.Despawn();
		}
        else
		{
			if (!other.TryGetComponent(out NetworkObject targetNetObj)) return;

			PlayerHealth pHealth = other.GetComponent<PlayerHealth>();

			if (pHealth == null) return;

			if (_savedTarget.Contains(targetNetObj.NetworkObjectId)) return;
			_savedTarget.Add(targetNetObj.NetworkObjectId);

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = attackerClientId,
				knockbackDir = direction,
				knockbackPower = 0f
			};

			if (pHealth != null) pHealth.TakeDamage(damageInfo);
		}
	}
}