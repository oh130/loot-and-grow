using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTableSO", menuName = "Data/LootTable")]
public class LootTableSO : ScriptableObject
{
    public List<LootEntry> items = new List<LootEntry>();

    public List<LootEntry> GetByLootId(int lootId) => items.FindAll(e => e.LootID == lootId);
}
