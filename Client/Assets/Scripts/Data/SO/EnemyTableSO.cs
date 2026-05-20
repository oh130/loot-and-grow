using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyTableSO", menuName = "Data/EnemyTable")]
public class EnemyTableSO : ScriptableObject
{
    public List<EnemyEntry> items = new List<EnemyEntry>();

    public EnemyEntry GetById(int id) => items.Find(e => e.EnemyID == id);
}
