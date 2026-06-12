// 로그인 성공 후 게임 전체에서 유저 정보를 보관하는 정적 클래스
// MonoBehaviour가 아니므로 씬이 바뀌어도 데이터가 유지됩니다
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public static class UserSession
{
    public static int DbUserId { get; set; }
    public static string Username { get; set; }
    public static string Role { get; set; }
    public static bool IsBanned { get; set; }
    public static string LastLoginAt { get; set; }

    public static string Token { get; set; }
    public static string RefreshToken { get; set; }

    public static bool IsLoggedIn => DbUserId != 0;
    public static bool IsAdmin => Role == "admin";

    public static string AppVersion { get; set; }

    public static void Apply(LoginResponse res)
    {
        DbUserId    = res.id;
        Username    = res.username;
        Role        = res.role;
        IsBanned    = res.is_banned;
        LastLoginAt = res.last_login_at;
        Token       = res.token;
        RefreshToken = res.refresh_token;
        AppVersion = Application.version;

        SaveToPrefs();
    }

    // ─── PlayerPrefs 저장/로드 ─────────────────────────────────────────────

    public static void SaveToPrefs()
    {
        if (string.IsNullOrEmpty(RefreshToken)) return;
        PlayerPrefs.SetString("session_refresh_token", RefreshToken);
        PlayerPrefs.SetInt   ("session_user_id",       DbUserId);
        PlayerPrefs.SetString("session_username",      Username ?? "");
        PlayerPrefs.SetString("session_role",          Role ?? "user");
        PlayerPrefs.Save();
    }

    public static string LoadRefreshTokenFromPrefs()
    {
        return PlayerPrefs.GetString("session_refresh_token", "");
    }

    public static void ClearPrefs()
    {
        PlayerPrefs.DeleteKey("session_refresh_token");
        PlayerPrefs.DeleteKey("session_user_id");
        PlayerPrefs.DeleteKey("session_username");
        PlayerPrefs.DeleteKey("session_role");
        PlayerPrefs.Save();
    }

    public static void ClearSession()
    {
        DbUserId     = 0;
        Username     = null;
        Role         = null;
        IsBanned     = false;
        LastLoginAt  = null;
        Token        = null;
        RefreshToken = null;
        AppVersion = null;
        ClearPrefs();
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

    public string AppVersion;

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
            AppVersion = UserSession.AppVersion
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
        serializer.SerializeValue(ref AppVersion);
    }
}
