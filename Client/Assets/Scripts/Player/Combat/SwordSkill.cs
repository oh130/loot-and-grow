using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class SwordSkill : NetworkBehaviour
{
	[SerializeField] private bool isEnemyAttack = false;
	[SerializeField] private bool isAIPlayerAttack = false;
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

		if(isEnemyAttack)
		{
			if (!other.TryGetComponent(out NetworkObject targetNetObj)) return;

			IHealth targetHealth = other.GetComponent<IHealth>();
			EnemyHealth targetEnemy = other.GetComponent<EnemyHealth>();

			if (targetHealth == null || targetEnemy != null) return; // 에너미끼리는 안 때리도록 검사

			if (_savedTarget.Contains(targetNetObj.NetworkObjectId)) return;
			_savedTarget.Add(targetNetObj.NetworkObjectId);

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = 0,
				knockbackDir = direction,
				knockbackPower = 0f
			};

			targetHealth?.TakeDamage(damageInfo);
		}
		else if(isAIPlayerAttack)
		{
			if (!other.TryGetComponent(out NetworkObject targetNetObj)) return;

			IHealth targetHealth = other.GetComponent<IHealth>();
			AIPlayerHealth targetAIPlayer = other.GetComponent<AIPlayerHealth>();
			if(targetHealth == null) return;
			if(targetAIPlayer != null && targetAIPlayer.NetworkObjectId == attackerClientId) return;

			if (_savedTarget.Contains(targetNetObj.NetworkObjectId)) return;
			_savedTarget.Add(targetNetObj.NetworkObjectId);

			DamageInfo damageInfo = new DamageInfo()
			{
				damage = damage,
				attackerClientId = 0,
				knockbackDir = direction,
				knockbackPower = 0f
			};

			targetHealth?.TakeDamage(damageInfo);
		}
        else
		{
			if (!other.TryGetComponent(out NetworkObject targetNetObj)) return;

			IHealth targetHealth = other.GetComponent<IHealth>();
			PlayerHealth targetPlayer = other.GetComponent<PlayerHealth>();

			if (targetHealth == null) return;
			if (targetPlayer != null && targetPlayer.OwnerClientId == attackerClientId) return;

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

			targetHealth?.TakeDamage(damageInfo);
			// if (NetworkObject != null && NetworkObject.IsSpawned)
			// 	NetworkObject.Despawn();
		}
	}
}