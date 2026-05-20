using DG.Tweening;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UI_Death : UI_CanToggle
{
    [SerializeField] private TextMeshProUGUI deathText;
    [SerializeField] private Button respawnButton;

    public void Init(string killerName)
    {
        deathText.text = $"{killerName}에 의해 사망했습니다.\n모든 장비와 일부 아이템 및 골드를 드롭합니다.";
        respawnButton.gameObject.SetActive(false);
    }

    public override void ToggleUI(bool shouldShow = false)
    {
        _isOpened = !_isOpened;
        if(shouldShow) _isOpened = true;

        if (_isOpened)
        {
            canvasGroup.DOFade(1f, 0.6f).OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            });
            DOVirtual.DelayedCall(3f, () => {respawnButton.gameObject.SetActive(true);});
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void OnClickRespawn()
    {
        ServerManager.Instance.RequestRespawnRpc();
    }
}
