using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class DroppedItem : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsInitialized = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString64Bytes> ItemName = new NetworkVariable<FixedString64Bytes>("");
    public NetworkVariable<int> ItemID = new NetworkVariable<int>(3000);

    [Header("Datas")]
    [SerializeField] private ItemTableSO itemTableSO;

    [Header("Components")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Rigidbody rigid;
    [SerializeField] private SpriteRenderer sprite;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiItemNamePrefab;

    private UI_Username _uiItemName;

    // 서버만 가지고 있으면 될 값
    private DroppedItemData _itemData;
    private bool _isHandling = false;

    private void Update()
    {
        if(IsServer)
        {
            transform.Rotate(Vector3.up, 90f * Time.deltaTime);
        }
    }

    public void Init(DroppedItemData itemData) // 서버에서만 호출됨
    {
        _itemData = itemData;
        ItemID.Value = itemData.ItemInfo.ItemID;

        AutoDestroy();
    }

    public void Init(DroppedItemData itemData, Vector3 throwDir) // 서버에서만 호출됨
    {
        _itemData = itemData;
        ItemID.Value = itemData.ItemInfo.ItemID;

        rigid.AddForce(throwDir * 100f);

        AutoDestroy();
    }

    private void AutoDestroy()
    {
        DOVirtual.DelayedCall(60f, () =>
        {
            gameObject.GetComponent<NetworkObject>().Despawn();
        }).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void SetVisualActive(bool isActive)
    {
        if(visualRoot != null) visualRoot.SetActive(isActive);
    }

    public override void OnNetworkSpawn()
    {
        IsInitialized.OnValueChanged += OnInitializedChanged;
        ItemName.OnValueChanged += OnItemNameChanged;
        ItemID.OnValueChanged += OnItemIDChanged;
        SetVisualActive(IsInitialized.Value);

        if(IsInitialized.Value)
        {
            var entry = itemTableSO.GetById(ItemID.Value);
            if (entry != null) sprite.sprite = entry.Icon;

            if(!string.IsNullOrEmpty(ItemName.Value.ToString()))
            {
                UpdateUIItemName(ItemName.Value.ToString());
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        IsInitialized.OnValueChanged -= OnInitializedChanged;
        ItemName.OnValueChanged -= OnItemNameChanged;
        ItemID.OnValueChanged -= OnItemIDChanged;
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            sprite.sprite = itemTableSO.GetById(ItemID.Value).Icon;
            SetVisualActive(true);
            UpdateUIItemName(ItemName.Value.ToString());
        }
    }

    private void OnItemNameChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        if(!IsInitialized.Value) return;
        UpdateUIItemName(newVal.ToString());
    }

    private void OnItemIDChanged(int oldVal, int newVal)
    {
        if(!IsInitialized.Value) return;
        sprite.sprite = itemTableSO.GetById(newVal).Icon;
    }
    
    private void UpdateUIItemName(string name)
    {
        if(string.IsNullOrEmpty(name)) return;

        if(_uiItemName != null)
        {
            _uiItemName.SetText(name);
            return;
        }

        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            GameObject go = PoolManager.Instance.Get("UI_ItemName", worldUI.transform);
            _uiItemName = go.GetComponent<UI_Username>();
            _uiItemName.Init(transform, name);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!IsServer || !IsInitialized.Value || _isHandling) return;

        ClientManager client = other.GetComponent<ClientManager>();
        if(client && !client.IsDead.Value)
        {
            _isHandling = true;
            ServerManager.Instance.AddItem(client.OwnerClientId, _itemData, NetworkObjectId);
        }
    }

    public void EndHandle(bool isSuccess)
    {
        if(!IsServer || !IsInitialized.Value || !_isHandling) return;
        
        PlayPickupSoundRPC();
        if(isSuccess)
            gameObject.GetComponent<NetworkObject>().Despawn();
        else
            _isHandling = false;
    }

    [Rpc(SendTo.Everyone)]
	private void PlayPickupSoundRPC()
    {
		SoundManager.Instance.PlaySFX3D("Pickup", transform.position);
    }
}
