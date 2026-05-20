using UnityEngine;

/// <summary>
/// TestScene 전용. 로그인 없이 UserSession을 수동으로 채워준다.
/// Inspector에서 userId, token 값을 직접 입력.
/// 빌드 전 이 오브젝트는 비활성화하거나 씬에서 제거할 것.
/// </summary>
public class TestBootstrap : MonoBehaviour
{
    [Header("테스트용 계정 정보 (서버 DB 값 직접 입력)")]
    [SerializeField] private int    userId = 1;
    [SerializeField] private string token  = "";  // 서버에서 발급받은 JWT 토큰

    private void Awake()
    {
        UserSession.DbUserId = userId;
        UserSession.Token    = token;
        Debug.Log($"[TestBootstrap] UserSession 세팅 완료 — userId={userId}");
    }
}
