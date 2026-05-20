using Unity.Netcode;
using UnityEngine;

public class SimpleEquipment : NetworkBehaviour
{
	[SerializeField] private Transform headSlot;
	[SerializeField] private GameObject[] hatPrefabs;

	private GameObject currentHat;
	private int currentHatIndex = -1;

	private void Update()
	{
		if (!IsOwner) return;

		// 숫자 키로 아이템 장착
		for (int i = 0; i < hatPrefabs.Length && i < 9; i++)
		{
			if (Input.GetKeyDown(KeyCode.Alpha1 + i))
			{
				RequestEquipHatServerRpc(i);
			}
		}

		// Q로 아이템 해제
		if (Input.GetKeyDown(KeyCode.Q))
		{
			RequestUnequipHatServerRpc();
		}
	}

	[ServerRpc]
	private void RequestEquipHatServerRpc(int index)
	{
		if (index < 0 || index >= hatPrefabs.Length) return;
		if (hatPrefabs[index] == null) return;

		if (currentHat != null)
		{
			currentHat.GetComponent<NetworkObject>().Despawn();
			currentHat = null;
		}

		GameObject hat = Instantiate(hatPrefabs[index], headSlot);
		hat.transform.localPosition = hatPrefabs[index].transform.localPosition;
		hat.transform.localRotation = hatPrefabs[index].transform.localRotation;
		hat.transform.localScale = hatPrefabs[index].transform.localScale;

		NetworkObject netObj = hat.GetComponent<NetworkObject>();
		netObj.Spawn(true);

		currentHat = hat;
		currentHatIndex = index;
	}

	[ServerRpc]
	private void RequestUnequipHatServerRpc()
	{
		if (currentHat == null) return;

		currentHat.GetComponent<NetworkObject>().Despawn();
		currentHat = null;
		currentHatIndex = -1;
	}
}