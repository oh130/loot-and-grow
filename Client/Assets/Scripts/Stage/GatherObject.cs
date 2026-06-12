using System;
using System.Collections;
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
    [SerializeField] private int lootID;
    [SerializeField] private string gatherObjectName;
    [SerializeField] private float gatherPercentPerSecond;
    [SerializeField] private float uiScaleFactor = 1f;
    [SerializeField] private Vector3 addOffset;
    [SerializeField] private bool isMotionOpen = false;
    [SerializeField] private bool isSelfRespawn = false;

    [Header("Components")]
    [SerializeField] private GameObject visualRoot;

    [Header("Prefabs")]
    [SerializeField] private GameObject droppedItem;
    [SerializeField] private NetworkObject spawnEffectPrefab;
    [SerializeField] private NetworkObject breakEffectPrefab;
    [SerializeField] private NetworkObject gatherObjectPrefab;

    private UI_Username _uiGatherObjectName;

    // 서버만 가질 정보
    private ulong _curGatheringClientId;
    private float _serverGatherProgress = 0f;
    private float _lastNetworkUpdateTime = 0f;

    public Action OnBreak;

    private void Update()
    {
        if(!IsServer) return;

        if(IsHandling.Value)
        {
            _serverGatherProgress += gatherPercentPerSecond * Time.deltaTime;
            
            if (Time.time - _lastNetworkUpdateTime >= 0.5f)
            {
                GatherPercent.Value = Mathf.Min(_serverGatherProgress, 100f);
                _lastNetworkUpdateTime = Time.time;
            }

            if(_serverGatherProgress >= 100f)
            {
                EndHandle(true);
            }
        }
    }

    private void SetVisualActive(bool isActive)
    {
        if(visualRoot != null) visualRoot.SetActive(isActive);
    }

    private IEnumerator DistanceCullingRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (GameManager.Instance.player == null) continue;

            float distance = Vector3.Distance(transform.position, GameManager.Instance.player.transform.position);

            if (distance > 50f)
            {
                SetVisualActive(false);
            }
            else
            {
                SetVisualActive(true);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        IsInitialized.OnValueChanged += OnInitializedChanged;
        GatherPercent.OnValueChanged += OnGatherPercentChanged;
        SetVisualActive(IsInitialized.Value);
        // StartCoroutine(DistanceCullingRoutine());

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
            DoSpawnEffect(true);
        }
    }

    private void DoSpawnEffect(bool flag)
	{
        if(!spawnEffectPrefab || !breakEffectPrefab) return;

		Quaternion spawnRot = Quaternion.LookRotation(transform.forward);

		NetworkObject effectObj = Instantiate(flag ? spawnEffectPrefab : breakEffectPrefab, transform.position, spawnRot);
		effectObj.Spawn(true);

		ServerManager.Instance.StartCoroutine(DespawnEffectAfterDelay(effectObj, 2f));
	}

	private IEnumerator DespawnEffectAfterDelay(NetworkObject effectObj, float delay)
	{
		yield return new WaitForSeconds(delay);

		if (effectObj != null && effectObj.IsSpawned)
		{
			effectObj.Despawn(true);
		}
	}

    public override void OnNetworkDespawn()
    {
        IsInitialized.OnValueChanged -= OnInitializedChanged;
        GatherPercent.OnValueChanged -= OnGatherPercentChanged;
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            SetVisualActive(true);
            UpdateUIItemName(gatherObjectName);
        }
    }

    private void OnGatherPercentChanged(float oldVal, float newVal)
    {
        _uiGatherObjectName?.SetHealth(newVal, 100f);
    }
    
    private void UpdateUIItemName(string name)
    {
        if(string.IsNullOrEmpty(name)) return;

        if(_uiGatherObjectName != null)
        {
            _uiGatherObjectName.SetText(name);
            _uiGatherObjectName?.SetHealth(0f, 100f);
            return;
        }

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            GameObject go = PoolManager.Instance.Get("UI_GatherObjectName", worldUI.transform);
            _uiGatherObjectName = go.GetComponent<UI_Username>();
            _uiGatherObjectName.Init(transform, name, true, true);
            _uiGatherObjectName.SetScaleAndOffset(uiScaleFactor, addOffset);
            _uiGatherObjectName?.SetHealth(0f, 100f);
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
            List<LootEntry> lootEntry = GameDataSO.Instance.LootTable.GetByLootId(lootID);
            
            bool isDropped = false;
            foreach(var entry in lootEntry)
            {
                if(entry.DropChance > Random.value * 100)
                {
                    isDropped = true;
                    Vector3 spawnPos = transform.position + new Vector3(Random.Range(-1f,1f), 2f, Random.Range(-1f,1f));
                    DroppedItem itemObject = Instantiate(droppedItem, spawnPos, Quaternion.identity).GetComponent<DroppedItem>();
                    int quantity = Random.Range(entry.MinQty, entry.MaxQty);

                    ItemEntry itemEntry = GameDataSO.Instance.ItemTable.GetById(entry.ItemID);

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
            if(!isDropped && isMotionOpen)
            {
                ServerManager.Instance?.SendTargetChat(gatherUserContext.ClientId, "상자가 비어있었습니다...");
            }

            if(lootID == 203)
                ServerManager.Instance?.SendChatBySystem("최상급 광맥이 채광되었습니다.");
            OnBreak?.Invoke();
            DoSpawnEffect(false);

            if(isSelfRespawn)
            {
                ServerManager.Instance.RespawnGatherObject(
                    gatherObjectPrefab, 
                    transform.position, 
                    transform.rotation, 
                    30f
                );
            }

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
