using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
	[SerializeField] private PlayerAnimSync animSync;
	[SerializeField] private ClientManager clientManager;
	public PlayerRuntimeStat playerRuntimeStat;
	// [SerializeField] private int maxHp = 100;

	public NetworkVariable<float> CurrentHp = new NetworkVariable<float>(
		0,
		NetworkVariableReadPermission.Everyone,
		NetworkVariableWritePermission.Server
	);

	public NetworkVariable<ulong> LastAttackerClientId = new NetworkVariable<ulong>(0);

	private float _lastHitTime = -999f;
	private float _lastHpRegenTime = -999f;
	private float _savedHpRegenAmount = 0f;

	public Action OnTakeDamage;

    private void Update()
    {
		if(!IsServer) return;
		if(!clientManager.IsInitialized.Value) return;

		if(Time.time >= _lastHitTime + 5f)
		{
			if(Time.time < _lastHpRegenTime + 1f) return;
			_lastHpRegenTime = Time.time;

			float hpRegenAmount = 0;
			_savedHpRegenAmount += playerRuntimeStat.HPR.Value;
			if(_savedHpRegenAmount > 1f)
			{
				hpRegenAmount = _savedHpRegenAmount;
				_savedHpRegenAmount = _savedHpRegenAmount - hpRegenAmount;
			}

			if(!clientManager.IsDead.Value)
			{
				if(hpRegenAmount > 0) Heal(hpRegenAmount);
			}
		}
    }

    // HP ��ȭ �̺�Ʈ ���
    public override void OnNetworkSpawn()
	{
		// if (IsServer)
		// {
		// 	CurrentHp.Value = maxHp;
		// }
		CurrentHp.OnValueChanged += OnHpChanged;
	}

	// HP ��ȭ �̺�Ʈ ����
	public override void OnNetworkDespawn()
	{
		CurrentHp.OnValueChanged -= OnHpChanged;
	}

	public void SetCurrentHpToMax() // ServerManager에서 PlayerRuntimeStat을 갱신한 이후, 최대체력 설정
	{
		if(!IsServer) return;

		CurrentHp.Value = playerRuntimeStat.MaxHP.Value;
	}

	public void SetCurrentHpLessThanOrEqualToMax() // 장비 갈아끼는 상황에서 현재 체력이 최대 체력을 넘으면 안 되니, 그때 사용
	{
		if(!IsServer) return;

		if(CurrentHp.Value > playerRuntimeStat.MaxHP.Value)
			CurrentHp.Value = playerRuntimeStat.MaxHP.Value;
	}

	private void OnHpChanged(float oldHp, float newHp)
	{
		if(IsServer)
		{
			UserContext context = ServerManager.Instance.UserContexts[OwnerClientId];
			if(context != null)
			{
				Debug.Log($"[PlayerHealth] 유저 {context.Username}(CID {OwnerClientId} / DID {context.DbUserId})의 CurrentHP: {oldHp} -> {newHp}");
			}
		}

		if (newHp < oldHp)
		{
			if (newHp <= 0)
			{
				OnDead();
			}
			else OnHit(oldHp, newHp);
		}
		
	}

	public void SetCurrentHp(float value) // 특정 값으로 설정
	{
		if(!IsServer) return;
		// if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료 확인

		value = Mathf.Min(value, playerRuntimeStat.MaxHP.Value);
		value = Mathf.Max(value, 0);
		CurrentHp.Value = value;
	}

	public void Heal(float value)
	{
		if(!IsServer) return;
		if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료 확인

		if(value > 0)
		{
			float prevValue = CurrentHp.Value;
			CurrentHp.Value = Mathf.Min(CurrentHp.Value + value, playerRuntimeStat.MaxHP.Value);
			if(prevValue != CurrentHp.Value) ShowDamageTextRpc(value, transform.position, false);
		}
	}

	public void TakeDamage(DamageInfo damageInfo)
	{
		if(!IsServer) return;
		if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료 확인
		if(clientManager.CurrentArea.Value == AreaType.PVE || clientManager.CurrentArea.Value == AreaType.Gather)
		{
			if(damageInfo.attackerClientId != 0) return; // PvE, Gather 구역에서는 에너미 공격만 맞기
		}
		else if(clientManager.CurrentArea.Value != AreaType.PVP) return;

		_lastHitTime = Time.time;

		float resultDamage = Mathf.Max(1.0f, damageInfo.damage / (1 + playerRuntimeStat.DEF.Value / 10f)); // 방어력 적용
		LastAttackerClientId.Value = damageInfo.attackerClientId;
		CurrentHp.Value = Mathf.Max(CurrentHp.Value - resultDamage, 0);

		OnTakeDamage?.Invoke();

		ShowDamageTextRpc(-resultDamage, transform.position, damageInfo.isCritical);
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

	private void OnHit(float oldHp, float newHp)
	{
		if (IsOwner)
		{
			if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료 확인
			animSync.RequestAnimationServerRpc(PlayerAnimationEnum.GetAttack);
			// Debug.Log("���� ���ݹ���"); // 텍스트 깨짐
			Debug.Log($"[PlayerHealth] CurrentHP: {oldHp} -> {newHp}");
		}
	}

	private void OnDead()
	{
		if (IsOwner)
		{
			if(!clientManager.IsInitialized.Value) return; // 초기 설정 완료 확인
			if(clientManager.IsDead.Value) return;
			ServerManager.Instance.RequestDieRpc(LastAttackerClientId.Value);
			Debug.Log("[PlayerHealth] Dead");
		}
	}
}