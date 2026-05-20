using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DashButtonBinder : MonoBehaviour
{
	[SerializeField] private Button dashButton;

	private PlayerController playerController;

	private void Start()
	{
		TryBindDash();
	}

	void TryBindDash()
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
			Invoke(nameof(TryBindDash), 0.5f);
			return;
		}

		playerController = localClient.PlayerObject.GetComponent<PlayerController>();

		if (playerController == null)
		{
			Debug.LogWarning("PlayerController 없음");
			return;
		}

		dashButton.onClick.RemoveAllListeners();
		dashButton.onClick.AddListener(playerController.OnDashButtonPressed);

		Debug.Log("Dash 버튼 연결 완료");
	}
}