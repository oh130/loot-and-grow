using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemTableSO", menuName = "Data/ItemTable")]
public class ItemTableSO : ScriptableObject
{
    public List<ItemEntry> items = new List<ItemEntry>();

    public ItemEntry GetById(int id) => items.Find(e => e.ItemID == id);
}
