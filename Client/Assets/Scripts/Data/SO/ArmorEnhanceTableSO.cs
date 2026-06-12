using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ArmorEnhanceTableSO", menuName = "Data/ArmorEnhanceTable")]
public class ArmorEnhanceTableSO : ScriptableObject
{
    public List<ArmorEnhanceEntry> items = new List<ArmorEnhanceEntry>();

    public ArmorEnhanceEntry GetBySlotAndLevel(string slotType, int level)
    {
        return items.Find(e =>
            string.Equals(e.SlotType, slotType, System.StringComparison.OrdinalIgnoreCase)
            && e.EnhanceLevel == level);
    }
}
