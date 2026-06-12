using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;

public class AIPlayerHealth : NetworkBehaviour, IHealth
{
    public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
		0,
		NetworkVariableReadPermission.Everyone,
		NetworkVariableWritePermission.Server
	);

    [SerializeField] private AIPlayer aiPlayer;

    private float _lastHitTime = -999f;
	private float _lastHpRegenTime = -999f;
	private float _savedHpRegenAmount = 0f;

    private void Update()
    {
		if(!IsServer) return;
        if(!aiPlayer.IsInitialized || aiPlayer.IsDead) return;

		if(Time.time >= _lastHitTime + 5f)
		{
			if(Time.time < _lastHpRegenTime + 1f) return;
			_lastHpRegenTime = Time.time;

			float hpRegenAmount = 0;
			_savedHpRegenAmount += aiPlayer.AIPlayerStat.HPR;
			if(_savedHpRegenAmount > 1f)
			{
				hpRegenAmount = _savedHpRegenAmount;
				_savedHpRegenAmount = _savedHpRegenAmount - hpRegenAmount;
			}

            if(hpRegenAmount > 0) Heal(hpRegenAmount);
		}
    }

    public override void OnNetworkSpawn()
	{
		CurrentHp.OnValueChanged += OnHpChanged;
	}

	public override void OnNetworkDespawn()
	{
		CurrentHp.OnValueChanged -= OnHpChanged;
	}

	public void Init()
	{
		if(!IsServer) return;

		CurrentHp.Value = aiPlayer.AIPlayerStat.MaxHP;
	}

	private void OnHpChanged(float oldHp, float newHp)
	{
		if (newHp < oldHp)
		{
			if (newHp <= 0)
			{
				OnDead();
			}
			
            OnHit(oldHp, newHp);
		}
		
        aiPlayer.UpdateHPBar(newHp);
	}

	private void Heal(float value)
	{
		if(!IsServer) return;
        if(!aiPlayer.IsInitialized || aiPlayer.IsDead) return;

		if(value > 0)
		{
			float prevValue = CurrentHp.Value;
			CurrentHp.Value = Mathf.Min(CurrentHp.Value + value, aiPlayer.AIPlayerStat.MaxHP);
			if(prevValue != CurrentHp.Value) ShowDamageTextRpc(value, transform.position, false);
		}
	}

	public void TakeDamage(DamageInfo damageInfo)
	{
		if(!IsServer) return;
        if(!aiPlayer.IsInitialized || aiPlayer.IsDead) return;

		_lastHitTime = Time.time;

		float resultDamage = Mathf.Max(1.0f, damageInfo.damage / (1 + aiPlayer.AIPlayerStat.DEF / 10f)); // 방어력 적용
		CurrentHp.Value = Mathf.Max(CurrentHp.Value - resultDamage, 0);

        PlayHitSoundRPC();
		ShowDamageTextRpc(-resultDamage, transform.position, damageInfo.isCritical);

        if (damageInfo.attackerClientId == 0 || !ServerManager.Instance.UserContexts.ContainsKey(damageInfo.attackerClientId)) return;
		GameObject attackerObject = ServerManager.Instance.UserContexts[damageInfo.attackerClientId].PlayerObject;
		if (attackerObject == null) return;
        aiPlayer.SetTarget(attackerObject.transform);
	}

	[Rpc(SendTo.Everyone)]
	public void ShowDamageTextRpc(float value, Vector3 position, bool isCritical)
	{
		GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
			GameObject go = PoolManager.Instance.Get("UI_DamageText", worldUI.transform);
			UI_DamageText uiText = go.GetComponent<UI_DamageText>();
			uiText.Show(value, position, isCritical);
        }
	}

	[Rpc(SendTo.Everyone)]
	private void PlayHitSoundRPC()
    {
		SoundManager.Instance.PlaySFX3D("EnemyHit", transform.position);
    }

	private void OnHit(float oldHp, float newHp)
	{
		if(IsServer)
		{
			if (newHp > 0)
            {
                if(newHp < oldHp) aiPlayer.HandleHit();
                aiPlayer.ServerHandleAnimation(PlayerAnimationEnum.GetAttack);
            }
		}
	}

	private void OnDead()
	{
        if(aiPlayer.IsDead) return;
        aiPlayer.ProcessDeath();
	}
}
