using System;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public enum AreaType { Spawn, Shop, PVE, Upgrade, Gather, PVP }

[Serializable]
public class AreaInfo
{
    public AreaType areaType;
    public Collider areaCollider;
}

public class AreaTrigger : NetworkBehaviour
{
    [SerializeField] private AreaType areaType;
    private void OnTriggerEnter(Collider other)
    {
        if(!IsServer) return;

        ClientManager client = other.GetComponent<ClientManager>();
        if(client)
        {
            ServerManager.Instance.MoveTargetArea(client.OwnerClientId, areaType, true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if(!IsServer) return;

        ClientManager client = other.GetComponent<ClientManager>();
        if(client)
        {
            ServerManager.Instance.MoveTargetArea(client.OwnerClientId, areaType, false);
        }
    }

    public static string GetKoreanName(AreaType areaType)
    {
        string retStr = "";
        switch(areaType)
        {
            case AreaType.Spawn:
                retStr = "시작의 구역";
                break;
            case AreaType.Shop:
                retStr = "상점 구역";
                break;
            case AreaType.PVE:
                retStr = "던전 구역";
                break;
            case AreaType.Upgrade:
                retStr = "강화 구역";
                break;
            case AreaType.Gather:
                retStr = "광산 구역";
                break;
            case AreaType.PVP:
                retStr = "전투 구역";
                break;
        }
        return retStr;
    }
}
