using System.Collections.Generic;
using Unity.Netcode;

public struct PlayerStat : INetworkSerializable
{
    public float   MaxHP;
    public float   ATK;
    public float   DEF;
    public float APS;
    public float SPD;
    public float HPR;
    public float CHC;

    public static PlayerStat Calculate(
        BaseStatSO baseStat,
        EquipmentDetailSO equipDetail,
        List<EquippedItemResponse> equippedItems,
        EnhanceTableSO enhanceTable = null)
    {
        PlayerStat stat = new()
        {
            MaxHP = baseStat.HP,
            ATK   = baseStat.ATK,
            DEF   = baseStat.DEF,
            APS   = baseStat.APS,
            SPD   = baseStat.SPD,
            HPR   = baseStat.HPR,
            CHC   = baseStat.CHC
        };

        if (equippedItems == null) return stat;

        foreach (var equipped in equippedItems)
        {
            EquipmentDetailEntry detail = equipDetail.GetById(equipped.item_id);
            if (detail == null) continue;

            stat.MaxHP += detail.HP;
            stat.ATK   += detail.ATK;
            stat.DEF   += detail.DEF;
            stat.APS   += detail.APS;
            stat.SPD   += detail.SPD;
            stat.HPR   += detail.HPR;

            if (enhanceTable == null || equipped.enhance_level <= 0) continue;
            bool isWeapon = detail.WeaponType == "Sword" || detail.WeaponType == "Staff" || detail.WeaponType == "Bow";
            if (!isWeapon) continue;

            stat.ATK += enhanceTable.GetCumulativeATKBonus(detail.WeaponType, equipped.enhance_level);
            stat.CHC += enhanceTable.GetCritBonus(equipped.enhance_level);
        }

        return stat;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MaxHP);
        serializer.SerializeValue(ref ATK);
        serializer.SerializeValue(ref DEF);
        serializer.SerializeValue(ref APS);
        serializer.SerializeValue(ref SPD);
        serializer.SerializeValue(ref HPR);
        serializer.SerializeValue(ref CHC);
    }

    public static string GetKoreanName(string engName)
    {
        string korName = "잘못된 입력";
        switch(engName)
        {
            case "HP":
                korName = "최대 체력";
                break;
            case "ATK":
                korName = "공격력";
                break;
            case "DEF":
                korName = "방어력";
                break;
            case "APS":
                korName = "공격 속도";
                break;
            case "SPD":
                korName = "이동 속도";
                break;
            case "HPR":
                korName = "체력 재생";
                break;
            case "CHC":
                korName = "치명타 확률";
                break;
        }

        return korName;
    }
}
