using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

public class GatherObject : NetworkBehaviour, IInteractable
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsInitialized = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString64Bytes> ObjectName = new NetworkVariable<FixedString64Bytes>("");
    public NetworkVariable<bool> IsHandling = new NetworkVariable<bool>(false);
    public NetworkVariable<float> GatherPercent = new NetworkVariable<float>(0f);

    [Header("Datas")]
    [SerializeField] private LootTableSO lootTableSO;
    [SerializeField] private ItemTableSO itemTableSO;
    [SerializeField] private int lootID;
    [SerializeField] private string gatherObjectName;
    [SerializeField] private float gatherPercentPerSecond;
    [SerializeField] private float uiScaleFactor = 1f;
    [SerializeField] private Vector3 addOffset;
    [SerializeField] private bool isMotionOpen = false;

    [Header("Components")]
    [SerializeField] private GameObject visualRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject droppedItem;

    private UI_Username _uiGatherObjectName;

    // 서버만 가질 정보
    private ulong _curGatheringClientId;

    public Action OnBreak;

    private void Update()
    {
        _uiGatherObjectName?.SetHealth(GatherPercent.Value, 100f);
        if(!IsServer) return;

        if(IsHandling.Value)
        {
            GatherPercent.Value += gatherPercentPerSecond * Time.deltaTime;
            if(GatherPercent.Value >= 100f)
            {
                EndHandle(true);
            }
        }
    }

    private void SetVisualActive(bool isActive)
    {
        if(visualRoot != null) visualRoot.SetActive(isActive);
    }

    public override void OnNetworkSpawn()
    {
        IsInitialized.OnValueChanged += OnInitializedChanged;
        SetVisualActive(IsInitialized.Value);

        if(IsInitialized.Value)
        {
            if(!string.IsNullOrEmpty(gatherObjectName))
            {
                UpdateUIItemName(gatherObjectName);
            }
        }

        if(IsServer)
        {
            IsInitialized.Value = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        IsInitialized.OnValueChanged -= OnInitializedChanged;
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            SetVisualActive(true);
            UpdateUIItemName(gatherObjectName);
        }
    }
    
    private void UpdateUIItemName(string name)
    {
        if(string.IsNullOrEmpty(name)) return;

        if(_uiGatherObjectName != null)
        {
            _uiGatherObjectName.SetText(name);
            return;
        }

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            GameObject go = PoolManager.Instance.Get("UI_GatherObjectName", worldUI.transform);
            _uiGatherObjectName = go.GetComponent<UI_Username>();
            _uiGatherObjectName.Init(transform, name, true, true);
            _uiGatherObjectName.SetScaleAndOffset(uiScaleFactor, addOffset);
        }
    }
    // ------------------------------------------------------------------
    public void Interact()
    {
        if(!IsInitialized.Value || IsHandling.Value) return;

        RequestStartGatherRpc(NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStartGatherRpc(ulong clientId)
    {
        IsHandling.Value = true;
        _curGatheringClientId = clientId;

        UserContext gatherUserContext = null;
		if(ServerManager.Instance)
		{
			gatherUserContext = ServerManager.Instance.UserContexts[_curGatheringClientId];
		}
		else return;
		if(gatherUserContext == null) return;

        if(lootID == 203)
            ServerManager.Instance?.SendChatBySystem("누군가 최상급 광맥을 채광하고 있습니다...");
        gatherUserContext.PlayerHealth.OnTakeDamage += OnGatheringPlayerTakeDamage;
		gatherUserContext.ClientManager.StartGatherRpc(isMotionOpen);
    }

    private void OnGatheringPlayerTakeDamage()
    {
        EndHandle(false);
    }

    private void EndHandle(bool isSuccess)
    {
        if(!IsServer || !IsInitialized.Value || !IsHandling.Value) return;
        
        UserContext gatherUserContext = null;
		if(ServerManager.Instance)
		{
			gatherUserContext = ServerManager.Instance.UserContexts[_curGatheringClientId]; // 종료시점의 정보확인을 통해 여전히 존재하는지 확인
		}
		else return;
		if(gatherUserContext != null)
        {
            gatherUserContext.PlayerHealth.OnTakeDamage -= OnGatheringPlayerTakeDamage;
		    gatherUserContext.ClientManager.EndGatherRpc();
        }

        if(isSuccess)
        {
            List<LootEntry> lootEntry = lootTableSO.GetByLootId(lootID);
            
            foreach(var entry in lootEntry)
            {
                if(entry.DropChance > Random.value * 100)
                {
                    Vector3 spawnPos = transform.position + new Vector3(Random.Range(-1f,1f), 2f, Random.Range(-1f,1f));
                    DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
                    int quantity = Random.Range(entry.MinQty, entry.MaxQty);

                    ItemEntry itemEntry = itemTableSO.GetById(entry.ItemID);

                    DroppedItemData itemData = new DroppedItemData(new ItemEntryNS(itemEntry), quantity, 0);
                    itemObject.GetComponent<NetworkObject>().Spawn();
                    itemObject.Init(itemData);
                    if(itemEntry.ItemType == "Equipment")
                        itemObject.ItemName.Value = $"{itemEntry.Name} (+0)";
                    else 
                        itemObject.ItemName.Value = $"{itemEntry.Name} ({quantity}개)";
                    itemObject.IsInitialized.Value = true;
                }
            }

            if(lootID == 203)
                ServerManager.Instance?.SendChatBySystem("최상급 광맥이 채광되었습니다.");
            OnBreak?.Invoke();
            gameObject.GetComponent<NetworkObject>().Despawn();
        }
        else
        {
            // GatherPercent.Value = 0f;
            IsHandling.Value = false;
        }
    }

    public void OnTriggerEnterInteractRange(Collider other)
    {
        if (other.TryGetComponent(out ClientManager client))
        {
            if (client.IsOwner)
            {
                GameManager.Instance?.HUD.SetState(HUDState.NearbyNPC, gameObject);
            }
        }
    }

    public void OnTriggerExitInteractRange(Collider other)
    {
        if (other.TryGetComponent(out ClientManager client))
        {
            if (client.IsOwner)
            {
                GameManager.Instance?.HUD.SetState(HUDState.Normal);
            }
        }
    }
}
