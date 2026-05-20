using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentDetailSO", menuName = "Data/EquipmentDetail")]
public class EquipmentDetailSO : ScriptableObject
{
    public List<EquipmentDetailEntry> items = new List<EquipmentDetailEntry>();

    public EquipmentDetailEntry GetById(int id) => items.Find(e => e.ItemID == id);
}
