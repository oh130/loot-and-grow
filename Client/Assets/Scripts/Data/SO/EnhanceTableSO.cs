using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnhanceTableSO", menuName = "Data/EnhanceTable")]
public class EnhanceTableSO : ScriptableObject
{
    public List<EnhanceEntry> items = new List<EnhanceEntry>();

    public EnhanceEntry GetByLevel(int level) => items.Find(e => e.EnhanceLevel == level);

    public float GetCumulativeATKBonus(string weaponType, int enhanceLevel)
    {
        float total = 0f;
        for (int lvl = 1; lvl <= enhanceLevel; lvl++)
        {
            EnhanceEntry e = GetByLevel(lvl);
            if (e == null) break;
            total += weaponType == "Sword" ? e.SwordAdd : weaponType == "Staff" ? e.StaffAdd : e.BowAdd;
        }
        return total;
    }

    public int GetCritBonus(int enhanceLevel)
    {
        if (enhanceLevel <= 0) return 0;
        EnhanceEntry e = GetByLevel(enhanceLevel);
        return e != null ? e.CritChance : 0;
    }
}
