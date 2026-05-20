using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public enum PlayerStatType {HP, ATK, DEF, APS, SPD, HPR, CHC}

public class PlayerRuntimeStat : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<float> MaxHP = new NetworkVariable<float>();
    public NetworkVariable<float> ATK = new NetworkVariable<float>();
    public NetworkVariable<float> DEF = new NetworkVariable<float>();
    public NetworkVariable<float> APS = new NetworkVariable<float>();
    public NetworkVariable<float> SPD = new NetworkVariable<float>();
    public NetworkVariable<float> HPR = new NetworkVariable<float>();
    public NetworkVariable<float> CHC = new NetworkVariable<float>();

    public NetworkVariable<float> ATK_INC = new NetworkVariable<float>(0);
    public NetworkVariable<float> DEF_INC = new NetworkVariable<float>(0);
    public NetworkVariable<float> APS_INC = new NetworkVariable<float>(0);
    public NetworkVariable<float> SPD_INC = new NetworkVariable<float>(0);
    public NetworkVariable<float> HPR_INC = new NetworkVariable<float>(0);
    public NetworkVariable<float> CHC_INC = new NetworkVariable<float>(0);

    private PlayerStat _playerStat;

    public Action<string, float, float> OnApplyBuff;

    public void ApplyStat(PlayerStat playerStat)
    {
        if(!IsServer) return;
        _playerStat = playerStat;

        MaxHP.Value = _playerStat.MaxHP;
        ATK.Value = _playerStat.ATK + ATK_INC.Value;
        DEF.Value = _playerStat.DEF + DEF_INC.Value;
        APS.Value = _playerStat.APS + APS_INC.Value;
        SPD.Value = _playerStat.SPD + SPD_INC.Value;
        HPR.Value = _playerStat.HPR + HPR_INC.Value;
        CHC.Value = _playerStat.CHC + CHC_INC.Value;
    }

    public void ApplyStat()
    {
        if(!IsServer) return;

        MaxHP.Value = _playerStat.MaxHP;
        ATK.Value = _playerStat.ATK + ATK_INC.Value;
        DEF.Value = _playerStat.DEF + DEF_INC.Value;
        APS.Value = _playerStat.APS + APS_INC.Value;
        SPD.Value = _playerStat.SPD + SPD_INC.Value;
        HPR.Value = _playerStat.HPR + HPR_INC.Value;
        CHC.Value = _playerStat.CHC + CHC_INC.Value;
    }

    public void ApplyBuffByItem(ConsumableDetailEntry consumableDetailEntry) // 일시적 능력치 상승
    {
        if(!IsServer) return;
        
        bool isSuccess = true;
        switch(consumableDetailEntry.EffectType)
        {
            case "APS":
                if(APS_INC.Value > 0)
                {
                    isSuccess = false;
                    break;
                }
                APS_INC.Value = consumableDetailEntry.EffectValue;
                break;
            case "SPD":
                if(SPD_INC.Value > 0)
                {
                    isSuccess = false;
                    break;
                }
                SPD_INC.Value = consumableDetailEntry.EffectValue;
                break;
        }

        if(isSuccess)
        {
            InvokeApplyBuffRPC(consumableDetailEntry.EffectType, consumableDetailEntry.EffectDuration, consumableDetailEntry.EffectValue);
            ApplyStat();
            ServerManager.Instance?.SendTargetChat(OwnerClientId, $"{consumableDetailEntry.Name}을(를) 사용했습니다. {PlayerStat.GetKoreanName(consumableDetailEntry.EffectType)}가 {consumableDetailEntry.EffectValue:F2} 증가했습니다. {consumableDetailEntry.EffectDuration}초동안 지속됩니다.");
            StartCoroutine(EndBuffByItem(consumableDetailEntry));
        }
        else
        {
            ServerManager.Instance?.SendTargetChat(OwnerClientId, $"{consumableDetailEntry.Name}을(를) 사용했으나, 이미 적용된 효과에 의해 무시되었습니다.");
        }
    }

    [Rpc(SendTo.Everyone)]
    private void InvokeApplyBuffRPC(string effectType, float duration, float value)
    {
        OnApplyBuff?.Invoke(effectType, duration, value);
    }

    private IEnumerator EndBuffByItem(ConsumableDetailEntry consumableDetailEntry)
    {
        if(!IsServer) yield break;

        yield return new WaitForSeconds(consumableDetailEntry.EffectDuration);

        switch(consumableDetailEntry.EffectType)
        {
            case "APS":
                APS_INC.Value = 0;
                break;
            case "SPD":
                SPD_INC.Value = 0;
                break;
        }

        ApplyStat();
        ServerManager.Instance?.SendTargetChat(OwnerClientId, $"{consumableDetailEntry.Name}의 효과가 끝났습니다.");
    }
}
