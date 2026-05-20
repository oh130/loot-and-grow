using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SkillButtonBinder : MonoBehaviour
{
	[SerializeField] private Button skillButton;

	private PlayerCombat playerCombat;

	private void Start()
	{
		TryBindSkill();
	}

	void TryBindSkill()
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
			Invoke(nameof(TryBindSkill), 0.5f);
			return;
		}

		playerCombat = localClient.PlayerObject.GetComponent<PlayerCombat>();

		if (playerCombat == null)
		{
			Debug.LogWarning("PlayerCombat ����");
			return;
		}

		skillButton.onClick.RemoveAllListeners();

		skillButton.onClick.AddListener(playerCombat.OnSkillButtonPressed);

		Debug.Log("Skill ��ư ���� �Ϸ�");
	}
}