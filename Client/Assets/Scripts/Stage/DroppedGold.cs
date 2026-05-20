using DG.Tweening;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class DroppedGold : NetworkBehaviour
{
    [Header("Network Variables")]
    public NetworkVariable<bool> IsInitialized = new NetworkVariable<bool>(false);
    public NetworkVariable<FixedString64Bytes> ItemName = new NetworkVariable<FixedString64Bytes>("");
    public NetworkVariable<int> Amount = new NetworkVariable<int>(0);

    [Header("Components")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Rigidbody rigid;
    [SerializeField] private SpriteRenderer sprite;

    [Header("Prefabs")]
    [SerializeField] private GameObject uiItemNamePrefab;

    private UI_Username _uiItemName;

    // 서버만 가지고 있으면 될 값
    private bool _isHandling = false;

    private void Update()
    {
        if(IsServer)
        {
            transform.Rotate(Vector3.up, 90f * Time.deltaTime);
        }
    }

    public void Init(int goldAmount) // 서버에서만 호출됨
    {
        Amount.Value = goldAmount;

        AutoDestroy();
    }

    public void Init(int goldAmount, Vector3 throwDir) // 서버에서만 호출됨
    {
        Amount.Value = goldAmount;

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
        SetVisualActive(IsInitialized.Value);

        if(IsInitialized.Value)
        {
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
    }

    private void OnInitializedChanged(bool oldVal, bool newVal)
    {
        if (newVal == true)
        {
            SetVisualActive(true);
            UpdateUIItemName(ItemName.Value.ToString());
        }
    }

    private void OnItemNameChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        if(!IsInitialized.Value) return;
        UpdateUIItemName(newVal.ToString());
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
            ServerManager.Instance.AddGold(client.OwnerClientId, Amount.Value, NetworkObjectId);
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
