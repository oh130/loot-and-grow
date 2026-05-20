using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AttackButtonBinder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
	[SerializeField] private Button attackButton;

	private PlayerCombat playerCombat;

	private void Start()
	{
		TryBindAttack();
	}

	void TryBindAttack()
	{
		if (NetworkManager.Singleton == null)
		{
			return;
		}

		var localClient = NetworkManager.Singleton.LocalClient;

		if (localClient == null)
		{
			return;
		}

		if (localClient.PlayerObject == null)
		{
			Invoke(nameof(TryBindAttack), 0.5f);
			return;
		}

		playerCombat = localClient.PlayerObject.GetComponent<PlayerCombat>();

		if (playerCombat == null)
		{
			Debug.LogWarning("PlayerCombat 없음");
			return;
		}

		attackButton.onClick.RemoveAllListeners();

		//attackButton.onClick.AddListener(playerCombat.OnAttackButtonPressed);

		Debug.Log("Attack 버튼 연결 완료");
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (playerCombat == null) return;

		playerCombat.OnAttackButtonDown();
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (playerCombat == null) return;

		playerCombat.OnAttackButtonUp();
	}
}