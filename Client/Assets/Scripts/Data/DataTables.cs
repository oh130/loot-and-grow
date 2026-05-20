using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 이 파일에는 [Serializable] 데이터 클래스만 정의
// ScriptableObject 클래스는 각자 파일에 분리 (Unity 규칙: 파일명 = 클래스명)

// ─── ConsumableDetail ──────────────────────────────────────────────────────
[Serializable]
public class ConsumableDetailEntry
{
    public int ItemID;
    public string Name;
    public string EffectType;
    public float EffectValue;
    public float EffectDuration;
    public float Cooltime;
}

// ─── EnemyTable ────────────────────────────────────────────────────────────
[Serializable]
public class EnemyEntry
{
    public int EnemyID;
    public string Name;
    public float HP;
    public float ATK;
    public float DEF;
    public float APS;
    public float SPD;
    public float HPR;
    public int LootID;
}

// ─── EnhanceTable ──────────────────────────────────────────────────────────
[Serializable]
public class EnhanceEntry
{
    public int   EnhanceLevel;
    public float SuccessRate;
    public float SwordAdd;
    public float StaffAdd;
    public float BowAdd;
    public int   CritChance;
    public int   ReqStone;
}

// ─── EquipmentDetail ───────────────────────────────────────────────────────
[Serializable]
public class EquipmentDetailEntry
{
    public int ItemID;
    public string Name;
    public string EquipType;
    public string WeaponType;
    public float HP;
    public float ATK;
    public float DEF;
    public float APS;
    public float SPD;
    public float HPR;
}

// ─── ItemTable ─────────────────────────────────────────────────────────────
[Serializable]
public class ItemEntry
{
    public int ItemID;
    public string Name;
    public string ItemType;
    public string Grade;
    public int Buy;      // 상점 구매 가격
    public int Sell;     // 상점 판매 가격
    public int MaxStack; // 슬롯 당 최대 보유 수량 (장비=1, 소모품/재료=설정값)
    public string Path; // JSON 파싱용 — DataImporter가 Icon 할당 후 런타임에는 불필요
    public Sprite Icon; // DataImporter가 에디터 시점에 Path로부터 로드해 할당
}

public struct ItemEntryNS : INetworkSerializable
{
    public int ItemID;
    public FixedString64Bytes Name;
    public FixedString32Bytes ItemType;
    public FixedString32Bytes Grade;

    public ItemEntryNS(ItemEntry itemEntry)
    {
        ItemID = itemEntry.ItemID;
        Name = itemEntry.Name;
        ItemType = itemEntry.ItemType;
        Grade = itemEntry.Grade;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemID);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref ItemType);
        serializer.SerializeValue(ref Grade);
    }
}

// ─── LootTable ─────────────────────────────────────────────────────────────
[Serializable]
public class LootEntry
{
    public int LootID;
    public int ItemID;
    public float DropChance;
    public int MinQty;
    public int MaxQty;
}
