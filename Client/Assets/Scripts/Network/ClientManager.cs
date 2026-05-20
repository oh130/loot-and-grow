using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientManager : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsInitialized = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString64Bytes> UserName = new NetworkVariable<FixedString64Bytes>("");
    public NetworkVariable<double> JoinServerTime = new NetworkVariable<double>();
    public NetworkVariable<AreaType> CurrentArea = new NetworkVariable<AreaType>(AreaType.Spawn);
    public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);

    [Header("Components")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerAnimSync animSync;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private GameObject minimapIndicatorArrow;
    [SerializeField] private Collider coll;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiUsernamePrefab;

    private bool _isDisconnecting = false;
    private UI_Username _uiUsername;

    private void Update()
    {
        if(IsOwner)
        {
            #if UNITY_EDITOR
            if(Input.GetKeyDown(KeyCode.P))
                ServerManager.Instance.RequestOpenAdminUIRpc();
            if(Input.GetKeyDown(KeyCode.K))
                ServerManager.Instance.RequestDieRpc(NetworkManager.Singleton.LocalClientId);
            if(Input.GetKeyDown(KeyCode.I))
                ServerManager.Instance.RequestGetInventoryRpc();
            #endif
        }
    }

    private void SetVisualActive(bool isActive)
    {
        if (visualRoot != null) visualRoot.SetActive(isActive);
    }

    private void SetDeathState(bool isDead)
    {
        coll.enabled = !isDead;
        controller.SetMovement(!isDead);

        if(isDead) animSync.SetAnimation("Death");
        //else animSync.SetAnimation("Reset");
    }

    public override void OnNetworkSpawn()
    {
        // 자신 포함 모든 플레이어 오브젝트에 대해서 작동
        IsDead.OnValueChanged += OnIsDeadChanged;
        SetDeathState(IsDead.Value);

        // 자신 제외 플레이어 오브젝트에 대해 작동
        if(!IsOwner)
        {
            IsInitialized.OnValueChanged += OnInitializedChanged;
            UserName.OnValueChanged += OnUsernameChanged;
            playerHealth.CurrentHp.OnValueChanged += OnHpChanged;

            SetVisualActive(IsInitialized.Value);

            minimapIndicatorArrow.SetActive(false);

            if(IsInitialized.Value)
            {
                if(!string.IsNullOrEmpty(UserName.Value.ToString()))
                {
                    UpdateUIUsernameAndHealth(UserName.Value.ToString());
                }
            }
        }

        if (IsOwner && NetworkManager.Singleton != null)
        {
            GameManager.Instance.RegisterPlayerObject(gameObject);
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;

            if(ServerManager.Instance)
            {
                UserSessionData data = UserSessionData.GetUserSessionData();
                if (!IsServer)
                    ServerManager.Instance.RegisterUserSessionRpc(data);
                else
                    ServerManager.Instance.RegisterServerAsUserSession(data, this);
            }
        }
        if (IsServer)
        {
            JoinServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        }
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= OnIsDeadChanged;

        if(!IsOwner)
        {
            IsInitialized.OnValueChanged -= OnInitializedChanged;
            UserName.OnValueChanged -= OnUsernameChanged;
            playerHealth.CurrentHp.OnValueChanged -= OnHpChanged;
        }

        if (!IsOwner && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
        }
    }

    private void OnDisconnect(ulong clientId)
    {
        if (_isDisconnecting) return;
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            _isDisconnecting = true;
            Debug.Log("[ClientManager] 서버와의 연결이 종료되었습니다. (추방 또는 서버 종료)");
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("AuthorizeScene");
        }
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            SetVisualActive(true);
            UpdateUIUsernameAndHealth(UserName.Value.ToString());
        }
    }

    public double GetPlayTime()
    {
        return NetworkManager.Singleton.ServerTime.Time - JoinServerTime.Value;
    }

    [Rpc(SendTo.Owner)]
    public void TeleportOwnerRpc(Vector3 targetPos)
    {
        Debug.Log("[ClientManager] 텔레포트 작동");
        StartCoroutine(TeleportProcess(targetPos));
    }

    private IEnumerator TeleportProcess(Vector3 targetPos)
    {
        controller.SetMovement(false);

        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
        {
            nt.Teleport(targetPos, transform.rotation, transform.localScale);
            transform.position = targetPos;
        }

        yield return new WaitForFixedUpdate();

        controller.SetMovement(true);
    }

    [Rpc(SendTo.Owner)]
    public void ToggleAdminUIRpc()
    {
        GameManager.Instance.ToggleAdminUI();
    }

    [Rpc(SendTo.Owner)]
    public void ToggleBlinderUIRpc()
    {
        GameManager.Instance.ToggleBlinderUI();
    }

    private void OnUsernameChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        if(!IsInitialized.Value) return;
        UpdateUIUsernameAndHealth(newVal.ToString());
    }
    
    private void OnHpChanged(float oldHp, float newHp)
    {
        if(!IsInitialized.Value) return;
        UpdateUIUsernameAndHealth(UserName.Value.ToString());
    }

    private void UpdateUIUsernameAndHealth(string name)
    {
        if(string.IsNullOrEmpty(name)) return;

        if(_uiUsername != null)
        {
            _uiUsername.SetText(name);
            _uiUsername.SetHealth(playerHealth.CurrentHp.Value, playerHealth.playerRuntimeStat.MaxHP.Value);
            return;
        }

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            GameObject go = Instantiate(uiUsernamePrefab, worldUI.transform);
            _uiUsername = go.GetComponent<UI_Username>();
            _uiUsername.Init(transform, name, true);
            _uiUsername.SetHealth(playerHealth.CurrentHp.Value, playerHealth.playerRuntimeStat.MaxHP.Value);
        }
    }

    private void OnIsDeadChanged(bool oldVal, bool newVal)
    {
        SetDeathState(newVal);
    }

    [Rpc(SendTo.Owner)]
    public void ProcessDeathRpc(FixedString64Bytes killerName) // 사망시 처리
    {
        animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Death); // 사망 애니메이션 요청
        GameManager.Instance.ProcessDeath(true, killerName.ToString());
    }

    [Rpc(SendTo.Owner)]
    public void ProcessRespawnRpc() // 리스폰시 처리
    {
        animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Reset); // 애니메이션 리셋 요청
        GameManager.Instance.ProcessDeath(false);
        controller.SetMovement(true);
    }

    [Rpc(SendTo.Owner)]
    public void StartGatherRpc(bool isOpen = false)
    {
        controller.SetMovement(false);
        if(isOpen)
            animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Open);
        else
            animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Gather);
    }

    [Rpc(SendTo.Owner)]
    public void EndGatherRpc()
    {
        animSync.RequestAnimationServerRpc(PlayerAnimationEnum.Reset);
        controller.SetMovement(true);
    }
}