// 로그인 성공 후 게임 전체에서 유저 정보를 보관하는 정적 클래스
// MonoBehaviour가 아니므로 씬이 바뀌어도 데이터가 유지됩니다
using Unity.Collections;
using Unity.Netcode;

public static class UserSession
{
    public static int DbUserId { get; set; }
    public static string Username { get; set; }
    public static string Role { get; set; }
    public static bool IsBanned { get; set; }
    public static string LastLoginAt { get; set; } // 서버에서 받은 그대로의 문자열 (예: "2026-03-28T12:34:56")

    public static string Token { get; set; }

    public static bool IsLoggedIn => DbUserId != 0;
    public static bool IsAdmin => Role == "admin";

    public static void Apply(LoginResponse res)
    {
        DbUserId    = res.id;
        Username    = res.username;
        Role        = res.role;
        IsBanned    = res.is_banned;
        LastLoginAt = res.last_login_at;
        Token       = res.token;
    }
}

public struct UserSessionData : INetworkSerializable
{
    public int DbUserId;
    public FixedString64Bytes Username;
    public FixedString32Bytes Role;
    public bool IsBanned;
    public FixedString64Bytes LastLoginAt;

    public FixedString512Bytes Token;

    public bool IsLoggedIn;
    public bool IsAdmin;

    public static UserSessionData GetUserSessionData()
    {
        UserSessionData data = new UserSessionData()
        {
            DbUserId = UserSession.DbUserId,
            Username = UserSession.Username,
            Role = UserSession.Role,
            IsBanned = UserSession.IsBanned,
            LastLoginAt = UserSession.LastLoginAt,
            Token = UserSession.Token,
            IsLoggedIn = UserSession.IsLoggedIn,
            IsAdmin = UserSession.IsAdmin,
        };
        return data;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref DbUserId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref Role);
        serializer.SerializeValue(ref IsBanned);
        serializer.SerializeValue(ref LastLoginAt);
        serializer.SerializeValue(ref Token);
        serializer.SerializeValue(ref IsLoggedIn);
        serializer.SerializeValue(ref IsAdmin);
    }
}
