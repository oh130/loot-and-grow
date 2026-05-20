using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableDetailSO", menuName = "Data/ConsumableDetail")]
public class ConsumableDetailSO : ScriptableObject
{
    public List<ConsumableDetailEntry> items = new List<ConsumableDetailEntry>();

    public ConsumableDetailEntry GetById(int id) => items.Find(e => e.ItemID == id);
}
