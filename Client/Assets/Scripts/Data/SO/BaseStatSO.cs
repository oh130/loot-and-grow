using UnityEngine;

/// <summary>
/// 플레이어 기본 능력치 (장비 보너스 적용 전 고정값)
/// 에디터에서 수정 가능 — 재빌드 없이 바로 반영됨
/// </summary>
[CreateAssetMenu(fileName = "BaseStatSO", menuName = "Data/BaseStat")]
public class BaseStatSO : ScriptableObject
{
    [Header("기본 능력치")]
    public float HP = 20;       // 수치 2당 체력 1칸 (총 10칸)
    public float ATK = 2;       // 기본 공격력
    public float DEF = 0;       // 기본 방어력

    [Header("기본 능력치 (소수점)")]
    public float APS = 1.0f;  // 초당 공격 횟수
    public float SPD = 5.0f;  // 이동 속도 (m/s)
    public float HPR = 1.0f;  // 초당 체력 재생
    public float CHC = 0f;    // 크리티컬 확률
}
