using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public enum NPCType { Normal, Shop, Upgrade, Gather}

public class NPC : MonoBehaviour, IInteractable
{
    [SerializeField] private string npcName = "NoName";
    [SerializeField] private NPCType npcType = NPCType.Normal;
    [SerializeField] private GameObject uiNpcNamePrefab;
    [SerializeField] private ShopPanel targetShopPanel;
    [SerializeField] private EnhancePanel targetEnhancePanel;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        GameObject worldUI = GameManager.Instance.worldUI;
        if(worldUI != null)
        {
            UI_Username go = Instantiate(uiNpcNamePrefab, worldUI.transform).GetComponent<UI_Username>();
            go.Init(transform, npcName);
        }
        else
        {
            Invoke(nameof(Init), 0.5f);
			return;
        }
    }

    public void Interact()
    {
        switch(npcType)
        {
            case NPCType.Shop:
                targetShopPanel?.Open();
                break;
            case NPCType.Upgrade:
                targetEnhancePanel?.Open();
                break;
            // case NPCType.Gather:
            //     break;
            default:
                ServerManager.Instance?.SendChatByUserRpc($"{npcName}과 상호작용함");
                break;
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
