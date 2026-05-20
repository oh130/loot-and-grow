using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode.Components;
using System;
using Unity.VisualScripting;
using System.Collections;
using Random = UnityEngine.Random;
using NUnit.Framework.Constraints;

public class ServerManager : NetworkBehaviour
{
    public static ServerManager Instance { get; private set; }

    public static Action<ChatData> OnChatReceived;

    [Header("Prefabs")]
    [SerializeField] private GameObject droppedItem;
    [SerializeField] private GameObject droppedGold;
    [Header("Data SOs")]
    [SerializeField] private BaseStatSO baseStatSO;
    [SerializeField] private EquipmentDetailSO equipmentDetailSO;
    [SerializeField] private ItemTableSO itemTableSO;
    [SerializeField] private ConsumableDetailSO consumableDetailSO;

    public Dictionary<ulong, UserContext> UserContexts = new Dictionary<ulong, UserContext>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if UNITY_SERVER || UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
    private void Start()
    {
        if (Application.isBatchMode) 
        {
           StartCoroutine(StartServer());
        }
    }
#endif

    public IEnumerator StartServer()
    {
        // DB서버에 어드민 계정으로 로그인
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.Login("admin", "admin", 
            onSuccess: res => {
                isSuccess = true;
                UserSession.Apply(res);
                Debug.Log("[ServerManager] DB 서버에 admin으로 로그인하였습니다.");
            },
            onError: err =>
            {
                Debug.Log("<color=red>[ServerManager] DB 서버에 admin으로 로그인하는데 실패했습니다.</color>");
            }
        ));
        if(!isSuccess) yield break;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", 7777); 
            Debug.Log("[ServerManager] Listen Address를 0.0.0.0으로 설정했습니다.");
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[ServerManager] Dedicated Server 시작 시도");
        if(NetworkManager.Singleton.StartServer())
        {
            Debug.Log("[ServerManager] Dedicated Server 시작 성공. MainGameScene 씬으로 이동합니다.");
            NetworkManager.Singleton.SceneManager.LoadScene("MainGameScene", LoadSceneMode.Single);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Debug.Log($"[ServerManager] 유저 접속 / Client ID: {clientId}");
        // Debug.Log($"[ServerManager] 현재 접속자 수: {NetworkManager.Singleton.ConnectedClients.Count}명");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Debug.Log($"[ServerManager] 유저 퇴장 / Client ID: {clientId}");
        if (UserContexts.TryGetValue(clientId, out UserContext context))
        {
            SendChatBySystem($"{context.Username}님이 퇴장했습니다.");
            if(clientId != 0) StartCoroutine(UpdateUserDBData(context));
            UserContexts.Remove(clientId);
            SendChatBySystem($"현재 접속자 수: {UserContexts.Count}명");
        }
        else
        {
            Debug.Log($"[ServerManager] 미등록 클라이언트(CID {clientId}) 퇴장");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    /// <summary>
    /// 서버 자체 기능 -------------------------------------------------------------------------------
    /// </summary>
    private bool IsAdmin(ulong clientId)
    {
        return UserContexts.TryGetValue(clientId, out UserContext context) && context.IsAdmin;
    }

    private IEnumerator UpdateUserDBData(UserContext userContext)
    {
        Vector3 lastPos = Vector3.zero;
        if(!userContext.ClientManager.IsDead.Value)
            lastPos = userContext.PlayerObject.transform.position;
        else lastPos = Vector3.zero;

        float current_hp = userContext.PlayerHealth.CurrentHp.Value;
        if(current_hp <= 0) current_hp = userContext.PlayerRuntimeStat.MaxHP.Value;

        var data = new CharacterUpdateData {
            pos_x = lastPos.x,
            pos_y = lastPos.y,
            pos_z = lastPos.z,
            current_hp = current_hp
        };
        yield return StartCoroutine(DataAPI.UpdateCharacter(userContext.DbUserId, data,
            onSuccess: response => {
                
            },
            onError: err =>
            {
                Debug.Log($"[ServerManager] 유저 {userContext.Username}(DID {userContext.DbUserId})의 위치 정보를 저장하려고 했으나 실패했습니다. error : {err}");
            }
        ));
    }

    /// <summary>
    /// 서버 자체 기능 종료 ---------------------------------------------------------------------------
    /// </summary>

    /// <summary>
    /// 어드민 기능 ----------------------------------------------------------------------------------
    /// </summary>
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestOpenAdminUIRpc(RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if(!IsAdmin(adminId)) return;

        UserContext adminContext = UserContexts[adminId];
        adminContext.ClientManager.ToggleAdminUIRpc();
        Debug.Log($"[ServerManager] 어드민 {adminContext.Username}(CID {adminContext.ClientId} / DID {adminContext.DbUserId})이 어드민 UI 오픈(토글)");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestUserListRpc(RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if(!IsAdmin(adminId)) return;

        StartCoroutine(RequestUserList(rpcParams));
    }

    private IEnumerator RequestUserList(RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 어드민 UI의 유저 리스트 요청자 (CID {adminId})이 관리 목록에 없습니다.");
            yield break;
        }

        List<UserData> userList = new List<UserData>();
        foreach(UserContext context in UserContexts.Values)
        {
            // 핑 계산
            float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(context.ClientId);
            
            // 접속 후 경과 시간 계산
            double playTime = context.ClientManager.GetPlayTime();
            TimeSpan t = TimeSpan.FromSeconds(playTime);
            
            UserData data = context.ToNetworkStruct();
            data.Ping = Mathf.RoundToInt(rtt);
            data.PlayTime = string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
            data.Position = context.PlayerObject.transform.position;
            data.CurrentArea = context.ClientManager.CurrentArea.Value;

            userList.Add(data);
        }

        bool isSuccess = false;
        List<DataAPI.AdminUserResponse> responseList = new List<DataAPI.AdminUserResponse>();
        yield return StartCoroutine(DataAPI.GetAllUsers(adminCtx.DbUserId,
            onSuccess: resList => { 
                isSuccess = true;
                responseList = resList;
            },
            onError: err =>
            {
                Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 DB에서 유저 리스트를 가져오려고 했으나 실패했습니다.");
            }
        ));
        if(!isSuccess) yield break;

        if (adminId != 0 && !NetworkManager.Singleton.ConnectedClientsIds.Contains(adminId))
        {
            Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})에게 어드민 UI의 유저 리스트를 전달하려고 했으나 이미 퇴장했습니다. 처리를 종료합니다.");
            yield break;
        }

        List<DBUserData> dbUserList = new List<DBUserData>();
        foreach(var data in responseList)
        {
            string formattedTime = data.last_login_at;
            if (DateTime.TryParse(data.last_login_at, out DateTime utcTime))
            {
                DateTime kstTime = utcTime.AddHours(9);
                formattedTime = kstTime.ToString("yyyy-MM-dd HH:mm:ss");
            }

            dbUserList.Add(new DBUserData()
            {
               DbUserId = data.id,
               LoginID = data.login_id,
               UserName = data.username,
               Role = data.role,
               IsBanned = data.is_banned,
               LastLoginAt = formattedTime
            });
        }
        
        UpdateUserListRpc(userList.ToArray(), dbUserList.ToArray(), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        //Debug.Log($"[ServerManager] 어드민 {adminContext.Username}(CID {adminContext.ClientId} / DID {adminContext.DbUserId})에게 유저 리스트 전송");
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UpdateUserListRpc(UserData[] users, DBUserData[] dbUsers, RpcParams rpcParams = default)
    {
        UI_Admin.Instance?.UpdateData(users, dbUsers);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestKickUserRpc(ulong targetClientId, FixedString128Bytes reason, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if(!IsAdmin(adminId)) return;
        if(targetClientId == adminId || targetClientId == 0) return;

        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx) || 
            !UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 추방 대상(CID {targetClientId}) 또는 어드민(CID {adminId})이 관리 목록에 없습니다.");
            return;
        }

        SendMessageToClientRpc($"서버로부터 추방됨. 추방 사유 : {reason}", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        NetworkManager.Singleton.DisconnectClient(targetClientId);

        Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})를 추방했습니다.");
        
        RequestUserListRpc(rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestBanUserRpc(ulong targetClientId, FixedString128Bytes reason, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if(!IsAdmin(adminId)) return;
        if(targetClientId == adminId || targetClientId == 0) return;

        StartCoroutine(RequestBanUser(targetClientId, reason, rpcParams));
    }

    private IEnumerator RequestBanUser(ulong targetClientId, FixedString128Bytes reason, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!UserContexts.TryGetValue(targetClientId, out UserContext userCtx) || 
            !UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 영구 추방 대상(CID {targetClientId}) 또는 어드민(CID {adminId})이 관리 목록에 없습니다.");
            yield break;
        }

        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateUserBanStatus(userCtx.DbUserId, adminCtx.DbUserId, true,
            onSuccess: res => {
                isSuccess = true;
                Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})를 영구 추방했습니다.");
            },
            onError: err => {
                Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})를 영구 추방하려고 했으나 실패했습니다.");
            }
        ));
        if(!isSuccess) yield break;

        if (!NetworkManager.Singleton.ConnectedClientsIds.Contains(targetClientId))
        {
            Debug.Log($"[ServerManager] 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})의 영구 추방이 성공했으나, 대상의 접속이 이미 종료되었습니다. 추방 프로세스를 종료합니다.");
            yield break;
        }

        SendMessageToClientRpc($"서버로부터 영구 추방됨. 추방 사유 : {reason}", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        NetworkManager.Singleton.DisconnectClient(targetClientId);
        
        RequestUserListRpc(rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestBanToggleByDBIdRpc(DBUserData dbData, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!IsAdmin(adminId)) return;
        if (dbData.DbUserId == 1) return;

        StartCoroutine(RequestBanToggleByDBId(dbData, rpcParams));
    }

    private IEnumerator RequestBanToggleByDBId(DBUserData dbData, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        int dbUserId = dbData.DbUserId;

        if (!UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 오프라인 유저 영구 추방 여부 변경 요청 어드민(CID {adminId})이 관리 목록에 없습니다.");
            yield break;
        }

        bool targetBanStatus = !dbData.IsBanned;
        string actionName = targetBanStatus ? "영구 추방" : "영구 추방 해제";

        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateUserBanStatus(dbUserId, adminCtx.DbUserId, targetBanStatus,
            onSuccess: res => {
                isSuccess = true;
                Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 오프라인 유저 {dbData.UserName}(DID {dbUserId})를 {actionName}했습니다.");
            },
            onError: err => Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 오프라인 유저 {dbData.UserName}(DID {dbUserId})를 {actionName}하려고 했으나 실패했습니다.")
        ));
        if (!isSuccess) yield break;

        if (targetBanStatus)
        {
            CheckAndKickOfflineActionUser(dbData, $"서버로부터 영구 추방됨.");
        }
        
        RequestUserListRpc(rpcParams);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void SendMessageToClientRpc(FixedString128Bytes message, RpcParams rpcParams = default)
    {
        Debug.Log($"[ServerManager] {message}");
        PlayerPrefs.SetString("DisconnectReason", message.ToString());
        PlayerPrefs.Save();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTeleportRpc(bool isTo, ulong targetClientId, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!IsAdmin(adminId)) return;
        if (targetClientId == adminId) return;

        if (!UserContexts.TryGetValue(targetClientId, out UserContext userCtx) || 
            !UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 텔레포트 대상(CID {targetClientId}) 또는 어드민(CID {adminId})이 관리 목록에 없습니다.");
            return;
        }

        string log = $"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})";

        if(isTo)
        {
            adminCtx.ClientManager.TeleportOwnerRpc(userCtx.ClientManager.transform.position);
            log += " 위치로 텔레포트.";
        }
        else
        {
            userCtx.ClientManager.TeleportOwnerRpc(adminCtx.ClientManager.transform.position);
            log += "를 자신의 위치로 텔레포트.";
        }
        Debug.Log(log);
        
        RequestUserListRpc(rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ChangeUserRoleRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!IsAdmin(adminId)) return;
        if (targetClientId == adminId || targetClientId == 0) return;

        StartCoroutine(ChangeUserRole(targetClientId, rpcParams));
    }

    private IEnumerator ChangeUserRole(ulong targetClientId, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!UserContexts.TryGetValue(targetClientId, out UserContext userCtx) || 
            !UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 어드민(CID {adminId}) 또는 역할 변경 대상자(CID {targetClientId})가 관리 목록에 없습니다.");
            yield break;
        }

        string targetRole = userCtx.IsAdmin? "user" : "admin";
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateUserRole(userCtx.DbUserId, adminCtx.DbUserId, targetRole,
            onSuccess: res => {
                isSuccess = true;
                Debug.Log($"<color=red>[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})의 역할을 {targetRole}로 변경했습니다.</color>");
            },
            onError: err => {
                Debug.Log($"<color=red>[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})의 역할을 {targetRole}로 변경하려고 했으나 실패했습니다.</color>");
            }
        ));
        if(!isSuccess) yield break;

        if (!NetworkManager.Singleton.ConnectedClientsIds.Contains(targetClientId))
        {
            Debug.Log($"[ServerManager] 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})의 역할 변경이 성공했으나, 대상의 접속이 종료되었습니다. 정보 갱신을 중단합니다.");
            yield break;
        }

        // _userContexts[targetClientId].IsAdmin = targetRole == "admin"? true : false;
        // userCtx.ClientManager.ChangeRoleInUserSessionRpc(targetRole);
        SendMessageToClientRpc($"서버로부터 추방됨. 추방 사유 : 역할 변경으로 인한 재접속 필요", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        NetworkManager.Singleton.DisconnectClient(targetClientId);

        Debug.Log($"[ServerManager] 역할 변경에 따라 유저 {userCtx.Username}(CID {targetClientId} / DID {userCtx.DbUserId})를 추방했습니다.");
        
        RequestUserListRpc(rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ChangeUserRoleByDBIdRpc(DBUserData dbData, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if (!IsAdmin(adminId)) return;
        if (dbData.DbUserId == 1) return;

        StartCoroutine(ChangeUserRoleByDBId(dbData, rpcParams));
    }

    private IEnumerator ChangeUserRoleByDBId(DBUserData dbData, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        int dbUserId = dbData.DbUserId;
        if (!UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 오프라인 유저 역할 변경 요청 어드민(CID {adminId})이 관리 목록에 없습니다.");
            yield break;
        }

        string targetRole = dbData.Role == "admin"? "user" : "admin";
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateUserRole(dbUserId, adminCtx.DbUserId, targetRole,
            onSuccess: res => {
                isSuccess = true;
                Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 오프라인 유저 {dbData.UserName}(DID {dbUserId})의 역할을 {targetRole}로 변경했습니다.");
            },
            onError: err => Debug.Log($"[ServerManager] 어드민 {adminCtx.Username}(CID {adminId} / DID {adminCtx.DbUserId})이 오프라인 유저 {dbData.UserName}(DID {dbUserId})의 역할을 {targetRole}로 변경하려고 했으나 실패했습니다.")
        ));
        if(!isSuccess) yield break;

        CheckAndKickOfflineActionUser(dbData, "역할 변경으로 인한 재접속 필요");
        RequestUserListRpc(rpcParams);
    }

    private void CheckAndKickOfflineActionUser(DBUserData dbData, string reason)
    {
        var targetEntry = UserContexts.FirstOrDefault(x => x.Value.DbUserId == dbData.DbUserId);
        
        if (targetEntry.Value != null)
        {
            ulong targetClientId = targetEntry.Key;
            SendMessageToClientRpc(reason, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            NetworkManager.Singleton.DisconnectClient(targetClientId);
            Debug.Log($"[ServerManager] 오프라인 명령 처리 중 접속이 확인된 유저 {dbData.UserName}(CID {targetClientId} / DID {dbData.DbUserId})를 추방했습니다.");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestKillUserRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        ulong adminId = rpcParams.Receive.SenderClientId;
        if(!IsAdmin(adminId)) return;
        if(targetClientId == adminId || targetClientId == 0) return;

        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx) || 
            !UserContexts.TryGetValue(adminId, out UserContext adminCtx))
        {
            Debug.Log($"[ServerManager] 킬 대상(CID {targetClientId}) 또는 어드민(CID {adminId})이 관리 목록에 없습니다.");
            return;
        }

        StartCoroutine(RequestDie(adminId, targetClientId));
    }

    /// <summary>
    /// 어드민 기능 종료 ---------------------------------------------------------------------------------------
    /// </summary>


    /// <summary>
    /// 공용 기능 ---------------------------------------------------------------------------------------------
    /// </summary>
    public void RegisterServerAsUserSession(UserSessionData sessionData, ClientManager clientManager)
    {
        ulong clientId = 0;
        string ip = "SERVER";

        PlayerRuntimeStat playerRuntimeStat = clientManager.gameObject.GetComponent<PlayerRuntimeStat>();
        PlayerStat curStat = PlayerStat.Calculate(baseStatSO, equipmentDetailSO, new List<EquippedItemResponse>());
        playerRuntimeStat.ApplyStat(curStat);
        Debug.Log($"[PlayerRuntimeStat] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId})의 HP:{curStat.MaxHP} ATK:{curStat.ATK} DEF:{curStat.DEF} APS:{curStat.APS:F2} SPD:{curStat.SPD:F2} HPR:{curStat.HPR:F2} CHC:{curStat.CHC:F2}");
        PlayerHealth playerHealth = clientManager.gameObject.GetComponent<PlayerHealth>();
        playerHealth.SetCurrentHpToMax();

        UserContext context = new UserContext {
            ClientId = clientId,
            IpAddress = ip,
            DbUserId = sessionData.DbUserId,
            Username = sessionData.Username,
            IsAdmin = sessionData.IsAdmin,
            PlayerObject = clientManager.gameObject,
            ClientManager = clientManager,
            PlayerRuntimeStat = playerRuntimeStat,
            PlayerHealth = playerHealth
        };
        UserContexts[clientId] = context;

        Debug.Log($"[ServerManager] 유저 {context.Username}(CID {clientId} / DID {context.DbUserId}) 정보 등록 성공");
        SendChatBySystem($"{context.Username}님이 접속했습니다.");
        SendChatBySystem($"현재 접속자 수: {UserContexts.Count}명");

        // clientManager.ToggleBlinderUIRpc();
        StartCoroutine(EndInitializeUser(context, Vector3.zero));
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RegisterUserSessionRpc(UserSessionData sessionData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if(clientId == 0) return;

        StartCoroutine(RegisterUserSession(sessionData, rpcParams));
    }

    private IEnumerator RegisterUserSession(UserSessionData sessionData, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        NetworkManager networkManager = NetworkManager.Singleton;

        // 토큰 유효성 검증
        bool isTokenValid = false;
        yield return StartCoroutine(DataAPI.VerifyToken(sessionData.Token.ToString(),
            onSuccess: res => {
                isTokenValid = true;
            },
            onError: _ => isTokenValid = false
        ));
    
        if (!networkManager.ConnectedClientsIds.Contains(clientId))
        {
            Debug.Log($"[ServerManager] 토큰 유효성 검증 중 유저 (CID {clientId}) 접속이 종료되었습니다. 정보 등록을 중단합니다.");
            yield break;
        }

        if(!isTokenValid)
        {
            Debug.Log($"<color=red>[ServerManager] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId})의 정보 등록 중 토큰 유효성 검증 실패!!! 해당 유저를 추방합니다.</color>");
            SendMessageToClientRpc("토큰 유효성 검증이 실패하여 연결을 종료합니다.", RpcTarget.Single(clientId, RpcTargetUse.Temp));
            networkManager.DisconnectClient(clientId);
            yield break;
        }

        // 중복 로그인 감지
        foreach(UserContext curContext in UserContexts.Values)
        {
            if(curContext.DbUserId == sessionData.DbUserId)
            {
                Debug.Log($"[ServerManager] 유저 {sessionData.Username}(기존 CID {curContext.ClientId} / DID {curContext.DbUserId})의 중복 로그인을 감지했습니다. 기존 로그인 클라이언트를 종료합니다.");
                SendMessageToClientRpc("다른 기기에서 로그인이 감지되어 연결을 종료합니다.", RpcTarget.Single(curContext.ClientId, RpcTargetUse.Temp));
                networkManager.DisconnectClient(curContext.ClientId);
                break;
            }
        }

        // IP 주소 가져오기
        string ip = "Unknown";
        var transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if(transport != null) ip = transport.GetEndpoint(clientId).Address.Split(':')[0];

        // 해당 유저의 플레이어 오브젝트에 달린 ClientManager 가져오기
        ClientManager clientManager = null;
        if(networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient userClient))
            clientManager = userClient.PlayerObject.gameObject.GetComponent<ClientManager>();

        // 해당 유저의 플레이어 오브젝트에 달린 PlayerRuntimeStat 가져오기
        PlayerRuntimeStat playerRuntimeStat = userClient.PlayerObject.gameObject.GetComponent<PlayerRuntimeStat>();
        PlayerHealth playerHealth = userClient.PlayerObject.gameObject.GetComponent<PlayerHealth>();
        
        if(userClient == null || clientManager == null || playerRuntimeStat == null || playerHealth == null)
        {
            Debug.Log($"<color=red>[ServerManager] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId}) 정보 등록 실패!!! 해당 유저를 추방합니다.</color>");
            SendMessageToClientRpc("유저 정보 등록이 실패하여 연결을 종료합니다.", RpcTarget.Single(clientId, RpcTargetUse.Temp));
            networkManager.DisconnectClient(clientId);
            yield break;
        }

        bool isSuccess = false;
        Vector3 prevPos = Vector3.zero;
        float prevCurrentHp = -1;
        yield return StartCoroutine(DataAPI.GetCharacter(sessionData.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                // 유저 캐릭터 위치 복원
                prevPos = new Vector3(res.pos_x, res.pos_y, res.pos_z);
                prevCurrentHp = res.current_hp;
            },
            onError: error =>
            {
                Debug.Log($"<color=red>[ServerManager] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId})의 캐릭터 정보 가져오기 실패!!! 해당 유저를 추방합니다. 오류 메세지 : {error}</color>");
                SendMessageToClientRpc("캐릭터 정보 가져오기에 실패하여 연결을 종료합니다.", RpcTarget.Single(clientId, RpcTargetUse.Temp));
                networkManager.DisconnectClient(clientId);
            }
        ));
        if(!isSuccess) yield break;

        // 유저 능력치 계산
        isSuccess = false;
        EquippedItemListResponse response = null;
        yield return StartCoroutine(DataAPI.GetEquipped(sessionData.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                response = res;
            },
            onError: error =>
            {
                Debug.Log($"<color=red>[ServerManager] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId})의 장비 정보 가져오기 실패!!! 해당 유저를 추방합니다. 오류 메세지 : {error}</color>");
                SendMessageToClientRpc("장비 정보 가져오기에 실패하여 연결을 종료합니다.", RpcTarget.Single(clientId, RpcTargetUse.Temp));
                networkManager.DisconnectClient(clientId);
            }
        ));
        if(!isSuccess) yield break;

        // 관리용 유저 데이터화
        UserContext context = new UserContext {
            ClientId = clientId,
            IpAddress = ip,
            DbUserId = sessionData.DbUserId,
            Token = sessionData.Token,
            Username = sessionData.Username,
            IsAdmin = sessionData.IsAdmin,
            PlayerObject = userClient.PlayerObject.gameObject,
            ClientManager = clientManager,
            PlayerRuntimeStat = playerRuntimeStat,
            PlayerHealth = playerHealth
        };

        // 데이터 등록
        UserContexts[clientId] = context;
        clientManager.UserName.Value = sessionData.Username;

        // 유저 능력치 적용 및 체력 초기화
        PlayerStat curStat = PlayerStat.Calculate(baseStatSO, equipmentDetailSO, response.items);
        playerRuntimeStat.ApplyStat(curStat);
        Debug.Log($"[ServerManager] 유저 {sessionData.Username}(CID {clientId} / DID {sessionData.DbUserId})의 HP:{curStat.MaxHP} ATK:{curStat.ATK} DEF:{curStat.DEF} APS:{curStat.APS:F2} SPD:{curStat.SPD:F2} HPR:{curStat.HPR:F2} CHC:{curStat.CHC:F2}");
        
        // 체력 복원 (처음 접속한다면 20으로 설정됨)
        playerHealth.SetCurrentHp(prevCurrentHp); // 체력이 0인 상태에서 퇴장시에는 자동으로 풀피로 맞춰서 DB에 데이터 저장되기에, 즉시 사망 X

        Debug.Log($"[ServerManager] 유저 {context.Username}(CID {clientId} / DID {context.DbUserId}) 정보 등록 및 위치 복원 성공");
        SendChatBySystem($"{context.Username}님이 접속했습니다.");
        SendChatBySystem($"현재 접속자 수: {UserContexts.Count}명");

        // 구역 정보 갱신
        GameManager.Instance.GetAreaAtPosition(prevPos, out AreaType areaType);
        MoveTargetArea(clientId, areaType, true, true);

        StartCoroutine(EndInitializeUser(context, prevPos));
    }

    private IEnumerator EndInitializeUser(UserContext userContext, Vector3 prevPos)
    {
        userContext.ClientManager.TeleportOwnerRpc(prevPos);
        yield return new WaitForSecondsRealtime(1f);
        userContext.ClientManager.ToggleBlinderUIRpc();
        userContext.ClientManager.IsInitialized.Value = true;
    }

    public void SendChatBySystem(FixedString128Bytes chatText, RpcParams rpcParams = default)
    {
        ChatData data = new ChatData()
        {
            ClientId = 0,
            UserName = $"<color=red>System</color>",
            SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ChatText = chatText
        };

        Debug.Log($"[ServerManager] {data.SendTime} / {data.UserName} : {chatText}");
        ReceiveChatRpc(data);
    }

    // [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    // public void SendChatBySelfRpc(FixedString128Bytes chatText, RpcParams rpcParams = default)
    // {
    //     ulong id = rpcParams.Receive.SenderClientId;

    //     if (!UserContexts.TryGetValue(id, out UserContext userCtx))
    //     {
    //         Debug.Log($"[ServerManager] 채팅 전송자(CID {id})이 관리 목록에 없습니다.");
    //         return;
    //     }

    //     ChatData data = new ChatData()
    //     {
    //         ClientId = 0,
    //         UserName = $"<color=red>System</color>",
    //         SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    //         ChatText = chatText
    //     };

    //     ReceiveTargetChatRpc(data, RpcTarget.Single(id, RpcTargetUse.Temp));
    // }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendChatByUserRpc(FixedString128Bytes chatText, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        if (!UserContexts.TryGetValue(id, out UserContext userCtx))
        {
            Debug.Log($"[ServerManager] 채팅 전송자(CID {id})이 관리 목록에 없습니다.");
            return;
        }

        ChatData data = new ChatData()
        {
            ClientId = id,
            UserName = userCtx.Username,
            SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ChatText = chatText
        };

        Debug.Log($"[ServerManager] {data.SendTime} / {data.UserName}(CID {id} / DID {userCtx.DbUserId}) : {chatText}");
        ReceiveChatRpc(data);
    }

    [Rpc(SendTo.Everyone)]
    public void ReceiveChatRpc(ChatData data)
    {
        OnChatReceived?.Invoke(data);
    }

    public void SendTargetChat(ulong targetClientId, FixedString512Bytes chatText)
    {
        ChatData data = new ChatData()
        {
            ClientId = 0,
            UserName = $"<color=red>System</color>",
            SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ChatText = chatText
        };

        ReceiveTargetChatRpc(data, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ReceiveTargetChatRpc(ChatData data, RpcParams rpcParams = default)
    {
        OnChatReceived?.Invoke(data);
    }

    public void MoveTargetArea(ulong targetClientId, AreaType areaType, bool isIn, bool isInitializing = false)
    {
        ulong id = targetClientId;
        if(!UserContexts.TryGetValue(id, out UserContext userCtx))
        {
            //Debug.Log($"[ServerManager] 구역 이동 대상자(CID {id})이 관리 목록에 없습니다.");
            return;
        }
        if(!isInitializing && !userCtx.ClientManager.IsInitialized.Value) return;


        if(isInitializing)
        {
            userCtx.ClientManager.CurrentArea.Value = areaType;
            SendTargetChat(id, $"현재 위치한 구역은 {AreaTrigger.GetKoreanName(areaType)}입니다.");
            Debug.Log($"[ServerManager] 유저 {userCtx.Username}(CID {userCtx.ClientId} / DID {userCtx.DbUserId})이 {AreaTrigger.GetKoreanName(areaType)}에 진입했습니다. (위치 복원)");
        }
        if(isIn)
        {
            if(userCtx.ClientManager.CurrentArea.Value == areaType) return;
            userCtx.ClientManager.CurrentArea.Value = areaType;
            SendTargetChat(id, $"{AreaTrigger.GetKoreanName(areaType)}에 진입하였습니다.");
            Debug.Log($"[ServerManager] 유저 {userCtx.Username}(CID {userCtx.ClientId} / DID {userCtx.DbUserId})이 {AreaTrigger.GetKoreanName(areaType)}에 진입했습니다.");
        }
        else
        {
            if(userCtx.ClientManager.CurrentArea.Value != areaType) return;
            userCtx.ClientManager.CurrentArea.Value = AreaType.PVP;
            SendTargetChat(id, $"{AreaTrigger.GetKoreanName(areaType)}에서 퇴장하였습니다. 주의하세요!");
            Debug.Log($"[ServerManager] 유저 {userCtx.Username}(CID {userCtx.ClientId} / DID {userCtx.DbUserId})이 {AreaTrigger.GetKoreanName(areaType)}에서 퇴장했습니다.");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestDieRpc(ulong killerClientId, RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        StartCoroutine(RequestDie(killerClientId, targetClientId));
    }

    private IEnumerator RequestDie(ulong killerClientId, ulong targetClientId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx) || 
            !UserContexts.TryGetValue(killerClientId, out UserContext killerCtx))
        {
            Debug.Log($"[ServerManager] 사망 대상(CID {targetClientId}) 또는 사망 원인자(CID {killerClientId})이 관리 목록에 없습니다.");
            yield break;
        }
        
        if(targetCtx.ClientManager.IsDead.Value) yield break;

        // 사망자 장비 정보 가져오기
        List<EquippedItemResponse> targetEquipItems = null;
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.GetEquipped(targetCtx.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                targetEquipItems = res.items;
            },
            onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 장비 조회에 실패하여 사망 처리를 종료합니다. error : {err}")
        ));
        if(!isSuccess) yield break;

        // 사망자의 UI에서 장착 장비 제거 및 능력치 갱신 처리 시작
        DeleteEquipRpc(targetEquipItems.Select(r => (FixedString32Bytes)r.slot_type).ToArray(), RpcTarget.Single(targetClientId, RpcTargetUse.Temp)); // 장착 장비 제거/능력치 갱신 처리 맡기기

        // 사망자 인벤토리 정보 가져오기
        List<InventoryItemResponse> targetInventoryItems = null;
        yield return StartCoroutine(DataAPI.GetInventory(targetCtx.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                targetInventoryItems = res.items;
            },
            onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 인벤토리 조회에 실패하여 사망 처리를 종료합니다. error : {err}")
        ));
        if(!isSuccess) yield break;

        // 인벤토리의 아이템 중 드롭할 아이템 결정
        int targetCount = (int)Math.Ceiling(targetInventoryItems.Count * 0.3f);
        List<InventoryItemResponse> selectedItems = targetInventoryItems.OrderBy(item => Guid.NewGuid()).Take(targetCount).ToList();

        // 드롭하기로 결정된 아이템을 인벤토리에서 제거
        foreach(var item in selectedItems)
        {
            isSuccess = false;
            yield return StartCoroutine(DataAPI.AdminDeleteItem(targetCtx.DbUserId, item.id,
                onSuccess: res => {
                    isSuccess = true;
                },
                onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 인벤토리 아이템 제거에 실패하여 사망 처리를 종료합니다. error : {err}")
            ));
            if(!isSuccess) yield break;
        }

        // 사망자의 골드 정보 가져오기
        int dropGoldAmount = 0;
        var characterUpdateData = new CharacterUpdateData {
            gold = 0
        };

        isSuccess = false;
        yield return StartCoroutine(DataAPI.GetCharacter(targetCtx.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                dropGoldAmount = (int)(res.gold * 0.3f);
                characterUpdateData.gold = Mathf.Max(0, res.gold - dropGoldAmount);
            },
            onError: error =>
            {
                Debug.Log($"<color=red>[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 캐릭터 정보 가져오기에 실패했습니다. error : {error}</color>");
            }
        ));
        if(!isSuccess) yield break;

        // 사망자 사망 처리
        targetCtx.ClientManager.IsDead.Value = true;
        targetCtx.PlayerHealth.SetCurrentHp(0); // 이미 IsDead가 true이기에 사망 처리가 다시 이루어지지 않음
        targetCtx.ClientManager.ProcessDeathRpc(killerClientId == 0? "적" : killerCtx.Username);

        Debug.Log($"[ServerManager] 유저 {killerCtx.Username}(CID {killerClientId} / DID {killerCtx.DbUserId})이 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})를 처치했습니다.");
        SendChatBySystem(killerClientId == 0? $"{targetCtx.Username}님이 사망했습니다." : $"{killerCtx.Username}님이 {targetCtx.Username}님을 처치했습니다.");

        // 스테이지에 아이템 드롭 (장착했던 장비 전부 + 인벤토리 아이템 30%)
        Vector3 spawnPos;
        foreach(var item in targetEquipItems)
        {
            selectedItems.Add(new InventoryItemResponse(){ item_id = item.item_id, quantity = 1, enhance_level = item.enhance_level});    
        }

        foreach(var item in selectedItems)
        {
            spawnPos = targetCtx.PlayerObject.transform.position + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f));
            DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
            ItemEntry itemEntry = itemTableSO.GetById(item.item_id);
            DroppedItemData itemData = new DroppedItemData(new ItemEntryNS(itemEntry), item.quantity, item.enhance_level);
            itemObject.GetComponent<NetworkObject>().Spawn();
            itemObject.Init(itemData);
            if(itemEntry.ItemType == "Equipment")
                itemObject.ItemName.Value = $"{itemEntry.Name} (+{item.enhance_level})";
            else 
                itemObject.ItemName.Value = $"{itemEntry.Name} ({item.quantity}개)";
            itemObject.IsInitialized.Value = true;
        }

        // 골드 30% 드롭
        if(dropGoldAmount > 0)
        {
            spawnPos = targetCtx.PlayerObject.transform.position + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f));
            DroppedGold goldObject = Instantiate(droppedGold, spawnPos, Quaternion.identity).GetComponent<DroppedGold>();
            goldObject.GetComponent<NetworkObject>().Spawn();
            goldObject.Init(dropGoldAmount);
            goldObject.ItemName.Value = $"{dropGoldAmount}골드";
            goldObject.IsInitialized.Value = true;
        }

        // 사망자의 골드 정보 갱신
        isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateCharacter(targetCtx.DbUserId, characterUpdateData,
            onSuccess: response => {
                isSuccess = true;
            },
            onError: err =>
            {
                Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 골드 정보를 갱신하려고 했으나 실패했습니다. error : {err}");
            }
        ));
        if(!isSuccess) yield break;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void DeleteEquipRpc(FixedString32Bytes[] slotType, RpcParams rpcParams = default)
    {
        foreach(var type in slotType)
        {
            InventoryManager.Instance?.EquipmentPanel.RequestDeleteEquip(type.ToString());
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestRespawnRpc(RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;

        StartCoroutine(HandleRespawn(targetClientId));
    }

    private IEnumerator HandleRespawn(ulong targetClientId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 리스폰 대상(CID {targetClientId})이 관리 목록에 없습니다.");
            yield break;
        }
        
        if(!targetCtx.ClientManager.IsDead.Value) yield break;

        targetCtx.ClientManager.TeleportOwnerRpc(Vector3.zero);
        targetCtx.ClientManager.ProcessRespawnRpc();
        targetCtx.PlayerHealth.SetCurrentHpToMax();
        yield return new WaitForSeconds(1f);
        targetCtx.ClientManager.IsDead.Value = false;

        Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})이 리스폰했습니다.");
    }

    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestGetInventoryRpc(RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        StartCoroutine(RequestGetInventory(targetClientId));
    }

    private IEnumerator RequestGetInventory(ulong targetClientId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 인벤토리 조회 대상(CID {targetClientId})이 관리 목록에 없습니다.");
            yield break;
        }

        List<InventoryItemResponse> targetInventoryItems = null;
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.GetInventory(targetCtx.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                targetInventoryItems = res.items;
            },
            onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 인벤토리 조회에 실패했습니다.")
        ));
        if(!isSuccess) yield break;

        string total = "";
        foreach(var item in targetInventoryItems)
        {
            total += $"슬롯 : {item.id} / 아이템 : {item.item_id} / 개수 : {item.quantity}\n";
        }
        
        ReceiveLogRpc(total, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceiveLogRpc(FixedString4096Bytes log, RpcParams rpcParams = default)
    {
        Debug.Log("[로그] " + log.ToString());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestAddItemRpc(DroppedItemData itemData, ulong itemObjectId, RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        StartCoroutine(RequestAddItem(targetClientId, itemData, itemObjectId));
    }

    public void AddItem(ulong targetClientId, DroppedItemData itemData, ulong itemObjectId)
    {
        StartCoroutine(RequestAddItem(targetClientId, itemData, itemObjectId));
    }

    private IEnumerator RequestAddItem(ulong targetClientId, DroppedItemData itemData, ulong itemObjectId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 인벤토리 아이템 추가 대상(CID {targetClientId})이 관리 목록에 없습니다.");
            yield break;
        }

        bool isEquip = false;
        if(itemData.ItemInfo.ItemType == "Equipment") isEquip = true;

        if(isEquip)
        {
            // bool isSuccess = false;
            // yield return StartCoroutine(DataAPI.AddEquipment(targetCtx.DbUserId, itemData.ItemInfo.ItemID, itemData.EnhanceLevel,
            //     onSuccess: res => {
            //         isSuccess = true;
            //     },
            //     onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 인벤토리 아이템 추가를 실패했습니다.")
            // ));
            // if(!isSuccess) yield break;
            
            // SendTargetChatRpc(targetClientId, $"{itemData.ItemInfo.Name}(+{itemData.EnhanceLevel})을(를) 획득했습니다.");
            // ReceiveLogRpc($"{itemData.ItemInfo.Name}(+{itemData.EnhanceLevel}) 획득", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            PickupEquipmentRPC(itemObjectId, itemData.ItemInfo.ItemID, itemData.EnhanceLevel, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }
        else
        {
            // bool isSuccess = false;
            // yield return StartCoroutine(DataAPI.AddItem(targetCtx.DbUserId, itemData.ItemInfo.ItemID, itemData.Quantity,
            //     onSuccess: res => {
            //         isSuccess = true;
            //     },
            //     onError: err => Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 인벤토리 아이템 추가를 실패했습니다.")
            // ));
            // if(!isSuccess) yield break;
            
            // SendTargetChatRpc(targetClientId, $"{itemData.ItemInfo.Name}을(를) {itemData.Quantity}개 획득했습니다.");
            // ReceiveLogRpc($"{itemData.ItemInfo.Name} {itemData.Quantity}개 획득", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            PickupItemRPC(itemObjectId, itemData.ItemInfo.ItemID, itemData.Quantity, itemData.ItemInfo.ItemType, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void PickupEquipmentRPC(ulong itemObjectId, int itemId, int enhanceLevel = 0, RpcParams rpcParams = default)
    {
        StartCoroutine(InventoryManager.Instance.PickupEquipment(itemObjectId, itemId, enhanceLevel));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void PickupItemRPC(ulong itemObjectId, int itemId, int quantity, FixedString32Bytes itemType, RpcParams rpcParams = default)
    {
        StartCoroutine(InventoryManager.Instance.PickupItem(itemObjectId, itemId, quantity, itemType.ToString()));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void EndAddItemRpc(ulong itemObjectId, bool isSuccess, FixedString128Bytes chatText, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        if (!UserContexts.TryGetValue(id, out UserContext userCtx))
        {
            Debug.Log($"[ServerManager] 대상자(CID {id})가 관리 목록에 없습니다.");
            return;
        }

        DroppedItem droppedItem = NetworkManager.Singleton.SpawnManager.SpawnedObjects[itemObjectId].gameObject?.GetComponent<DroppedItem>();
        if(droppedItem != null)
            droppedItem.EndHandle(isSuccess);
        else
            Debug.Log($"[ServerManager] 처리 대상 DroppedItem 오브젝트가 존재하지 않습니다.");
        if(isSuccess) SendTargetChat(id, chatText);
    }

    private bool _goldChangeLock = false;
    public void AddGold(ulong targetClientId, int goldAmount, ulong itemObjectId)
    {
        StartCoroutine(RequestAddGold(targetClientId, goldAmount, itemObjectId));
    }

    private IEnumerator RequestAddGold(ulong targetClientId, int goldAmount, ulong itemObjectId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 골드 획득 대상(CID {targetClientId})이 관리 목록에 없습니다.");
            yield break;
        }

        while(_goldChangeLock) yield return null;
        _goldChangeLock = true;

        int curGold = 0;
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.GetCharacter(targetCtx.DbUserId,
            onSuccess: res => {
                isSuccess = true;
                curGold = res.gold;
            },
            onError: error =>
            {
                Debug.Log($"<color=red>[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 캐릭터 정보 가져오기에 실패했습니다. error : {error}</color>");
                _goldChangeLock = false;
            }
        ));
        if(!isSuccess) yield break;

        var characterUpdateData = new CharacterUpdateData {
            gold = curGold + goldAmount
        };

        isSuccess = false;
        yield return StartCoroutine(DataAPI.UpdateCharacter(targetCtx.DbUserId, characterUpdateData,
            onSuccess: response => {
                isSuccess = true;
            },
            onError: err =>
            {
                Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 골드 정보를 갱신하려고 했으나 실패했습니다. error : {err}");
                _goldChangeLock = false;
            }
        ));
        if(!isSuccess) yield break;

        DroppedGold droppedGold = NetworkManager.Singleton.SpawnManager.SpawnedObjects[itemObjectId].gameObject?.GetComponent<DroppedGold>();
        if(droppedGold != null)
            droppedGold.EndHandle(isSuccess);
        else
            Debug.Log($"[ServerManager] 처리 대상 DroppedGold 오브젝트가 존재하지 않습니다.");
        if(isSuccess) SendTargetChat(targetClientId, $"{goldAmount}골드를 획득했습니다.");

        _goldChangeLock = false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestApplyStatRpc(PlayerStat playerStat, RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;

        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 능력치 변경 대상(CID {targetClientId})이 관리 목록에 없습니다.");
            return;
        }
        
        targetCtx.PlayerRuntimeStat.ApplyStat(playerStat);
        targetCtx.PlayerHealth.SetCurrentHpLessThanOrEqualToMax();
        Debug.Log($"[ServerManager] 유저 {targetCtx.Username}(CID {targetClientId} / DID {targetCtx.DbUserId})의 HP:{playerStat.MaxHP} ATK:{playerStat.ATK} DEF:{playerStat.DEF} APS:{playerStat.APS:F2} SPD:{playerStat.SPD:F2} HPR:{playerStat.HPR:F2} CHC:{playerStat.CHC:F2}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestThrowItemRpc(DroppedItemData itemData, RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        StartCoroutine(RequestThrowItem(itemData, targetClientId));
    }

    private IEnumerator RequestThrowItem(DroppedItemData itemData, ulong targetClientId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 아이템 버리기 대상자(CID {targetClientId})가 관리 목록에 없습니다.");
            yield break;
        }

        // 인벤토리에서 제거하는 처리는 InventoryManager에서 처리함. 이 함수는 스테이지에 아이템을 드롭하는 처리
        Transform playerTransform = targetCtx.PlayerObject.transform;
        Vector3 spawnPos = targetCtx.PlayerObject.transform.position + playerTransform.forward + Vector3.up;
        DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
        itemObject.GetComponent<NetworkObject>().Spawn();
        itemObject.Init(itemData, playerTransform.forward);
        if(itemData.ItemInfo.ItemType == "Equipment")
            itemObject.ItemName.Value = $"{itemData.ItemInfo.Name} (+{itemData.EnhanceLevel})";
        else 
            itemObject.ItemName.Value = $"{itemData.ItemInfo.Name} ({itemData.Quantity}개)";
        itemObject.IsInitialized.Value = true;

        if(itemData.ItemInfo.ItemType == "Equipment")
        {
            SendTargetChat(targetClientId, $"{itemData.ItemInfo.Name}(+{itemData.EnhanceLevel})을(를) 바닥에 버렸습니다.");
            ReceiveLogRpc($"{itemData.ItemInfo.Name}(+{itemData.EnhanceLevel}) 버림", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }
        else
        {
            SendTargetChat(targetClientId, $"{itemData.ItemInfo.Name}을(를) 바닥에 {itemData.Quantity}개 버렸습니다.");
            ReceiveLogRpc($"{itemData.ItemInfo.Name} {itemData.Quantity}개 버림", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestApplyItemEffectRpc(int itemId, RpcParams rpcParams = default)
    {
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        StartCoroutine(RequestApplyItemEffect(itemId, targetClientId));
    }

    private IEnumerator RequestApplyItemEffect(int itemId, ulong targetClientId)
    {
        if (!UserContexts.TryGetValue(targetClientId, out UserContext targetCtx))
        {
            Debug.Log($"[ServerManager] 소비 아이템 효과 적용 대상자(CID {targetClientId})가 관리 목록에 없습니다.");
            yield break;
        }

        ConsumableDetailEntry consumableDetailEntry = consumableDetailSO.GetById(itemId);
        if(consumableDetailEntry.EffectType == "HP")
        {
            if(!targetCtx.ClientManager.IsDead.Value)
                targetCtx.PlayerHealth.Heal((int)consumableDetailEntry.EffectValue);
            
            SendTargetChat(targetClientId, $"{consumableDetailEntry.Name}을(를) 사용했습니다. 체력이 {consumableDetailEntry.EffectValue:F2} 회복되었습니다.");
        }
        else
        {
            targetCtx.PlayerRuntimeStat.ApplyBuffByItem(consumableDetailEntry);
        }
        ReceiveLogRpc($"{consumableDetailEntry.Name} 사용", RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
    }

    /// <summary>
    /// 공용 기능 종료 -------------------------------------------------------------------------------------------------------
    /// </summary>
}

/// <summary>
/// 서버 내부에서 가지고 있는 관리용 유저 데이터
/// </summary>
public class UserContext
{
    // NGO 관련
    public ulong ClientId;
    public string IpAddress;
    
    // 인증 서버 관련
    public int DbUserId;
    public FixedString512Bytes Token;
    public FixedString64Bytes Username;
    public bool IsAdmin;
    
    // 인게임 관련
    public GameObject PlayerObject;
    public ClientManager ClientManager;
    public PlayerRuntimeStat PlayerRuntimeStat;
    public PlayerHealth PlayerHealth;

    public UserData ToNetworkStruct()
    {
        return new UserData {
            ClientId = this.ClientId,
            IPAddress = this.IpAddress,
            DbUserId = this.DbUserId,
            UserName = this.Username,
            IsAdmin = this.IsAdmin
        };
    }
}

/// <summary>
/// Rpc 전송용 유저 데이터 (어드민 클라이언트의 어드민 UI 사용시 전송)
/// </summary>
public struct UserData : INetworkSerializable
{
    public ulong ClientId;
    public int DbUserId;
    public FixedString64Bytes UserName;
    public bool IsAdmin;
    public int Ping;
    public FixedString32Bytes PlayTime;
    public FixedString32Bytes IPAddress;
    public Vector3 Position;
    public AreaType CurrentArea;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref DbUserId);
        serializer.SerializeValue(ref UserName);
        serializer.SerializeValue(ref IsAdmin);
        serializer.SerializeValue(ref Ping);
        serializer.SerializeValue(ref PlayTime);
        serializer.SerializeValue(ref IPAddress);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref CurrentArea);
    }
}

/// <summary>
/// Rpc 전송용 유저 DB 데이터 (어드민 클라이언트의 어드민 UI 사용시 전송)
/// </summary>
public struct DBUserData : INetworkSerializable
{
    public int DbUserId;
    public FixedString64Bytes LoginID;
    public FixedString64Bytes UserName;
    public FixedString32Bytes Role;
    public bool IsBanned;
    public FixedString64Bytes LastLoginAt;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref DbUserId);
        serializer.SerializeValue(ref LoginID);
        serializer.SerializeValue(ref UserName);
        serializer.SerializeValue(ref Role);
        serializer.SerializeValue(ref IsBanned);
        serializer.SerializeValue(ref LastLoginAt);
    }
}

/// <summary>
/// 채팅 전송용 데이터
/// </summary>
public struct ChatData : INetworkSerializable
{
    public ulong ClientId;
    public FixedString64Bytes UserName;
    public FixedString32Bytes SendTime;
    public FixedString512Bytes ChatText;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref UserName);
        serializer.SerializeValue(ref SendTime);
        serializer.SerializeValue(ref ChatText);
    }
}