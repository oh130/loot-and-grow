using UnityEngine;

public class WeaponEquipment : MonoBehaviour
{
	[SerializeField] private Transform weaponSlot;
	[SerializeField] private GameObject[] weaponPrefabs;

	private GameObject currentWeapon;
	private int currentWeaponIndex = -1;

	private void Update()
	{
		// 숫자 키로 무기 장착
		for (int i = 0; i < weaponPrefabs.Length && i < 9; i++)
		{
			if (Input.GetKeyDown(KeyCode.Alpha1 + i))
			{
				EquipWeapon(i);
			}
		}

		// Q로 무기 해제
		if (Input.GetKeyDown(KeyCode.Q))
		{
			UnequipWeapon();
		}
	}

	private void EquipWeapon(int index)
	{
		if (index < 0 || index >= weaponPrefabs.Length) return;
		if (weaponPrefabs[index] == null) return;
		if (weaponSlot == null) return;

		if (currentWeapon != null)
		{
			Destroy(currentWeapon);
			currentWeapon = null;
		}

		GameObject prefab = weaponPrefabs[index];
		GameObject weapon = Instantiate(prefab, weaponSlot);

		weapon.transform.localPosition = prefab.transform.localPosition;
		weapon.transform.localRotation = prefab.transform.localRotation;
		weapon.transform.localScale = prefab.transform.localScale;

		currentWeapon = weapon;
		currentWeaponIndex = index;

		Debug.Log($"Weapon equipped: {weapon.name}");
	}

	private void UnequipWeapon()
	{
		if (currentWeapon == null) return;

		Destroy(currentWeapon);
		currentWeapon = null;
		currentWeaponIndex = -1;

		Debug.Log("Weapon unequipped");
	}
}