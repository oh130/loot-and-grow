using Unity.Netcode;
using UnityEngine;

public class StaffSkill : NetworkBehaviour
{
	private float lifeTime = 4.8f;

	private ulong attackerClientId;
	private float damage;
	private Vector3 direction;
	private bool initialized = false;

	private float _lastStepTime;
    [SerializeField] private float stepCooldown = 0.5f;

    private void Awake()
    {
        _lastStepTime = Time.time;
    }

    public void Initialize(ulong attackerId, float atk, Vector3 dir)
	{
		attackerClientId = attackerId;
		damage = atk;
		direction = dir.normalized;
		initialized = true;
	}

    private void Update()
    {
        if (Time.time - _lastStepTime < stepCooldown) return;
		_lastStepTime = Time.time;

		SoundManager.Instance.PlaySFX3D("StaffSkillParticle", transform.position);
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

	private void OnParticleCollision(GameObject other)
	{
		if (!IsServer || !initialized) return;
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
		// if (NetworkObject != null && NetworkObject.IsSpawned)
		// 	NetworkObject.Despawn();
	}
}