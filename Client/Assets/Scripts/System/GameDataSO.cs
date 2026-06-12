using UnityEngine;

/// <summary>
/// 씬에 하나만 배치하는 SO 레지스트리 싱글턴.
/// Inspector에서 SO를 한 번만 연결하면 모든 스크립트에서 참조 가능.
/// </summary>
public class GameDataSO : MonoBehaviour
{
    public static GameDataSO Instance { get; private set; }

    [Header("데이터 SO (Inspector에서 연결)")]
    public BaseStatSO          BaseStat;
    public ItemTableSO         ItemTable;
    public EquipmentDetailSO   EquipDetail;
    public ConsumableDetailSO  ConsumDetail;
    public EnhanceTableSO      EnhanceTable;
    public ArmorEnhanceTableSO ArmorEnhanceTable;
    public EnemyTableSO        EnemyTable;
    public LootTableSO         LootTable;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
