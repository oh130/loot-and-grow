using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerEquipmentVisual : NetworkBehaviour
{
	[Header("Slots")]
	[SerializeField] private Transform headSlot;
	[SerializeField] private Transform weaponSlot;
	[SerializeField] private Transform bowSlot;

	[Header("Skeleton (Skinned Mesh)")]
	[SerializeField] private Transform targetSkeletonRoot;
	[SerializeField] private Transform clothingParent;

	[Header("Mapping: Hat")]
	[SerializeField] private int[] hatItemIds;
	[SerializeField] private GameObject[] hatPrefabs;

	[Header("Mapping: Weapon")]
	[SerializeField] private int[] weaponItemIds;
	[SerializeField] private GameObject[] weaponPrefabs;

	[Header("Mapping: Top")]
	[SerializeField] private int[] topItemIds;
	[SerializeField] private SkinnedMeshRenderer[] topPrefabs;

	[Header("Mapping: Bottom")]
	[SerializeField] private int[] bottomItemIds;
	[SerializeField] private SkinnedMeshRenderer[] bottomPrefabs;

	[Header("Mapping: Accessory")]
	[SerializeField] private int[] accessoryItemIds;
	[SerializeField] private SkinnedMeshRenderer[] accessoryPrefabs;

	[Header("Reference")]
	[SerializeField] private PlayerCombat playerCombat;

	private GameObject currentHat;
	private GameObject currentWeapon;

	// ─── 외부 호출용 ─────────────────────────────

	// 핵심: 상태를 네트워크 변수로 관리
	private NetworkVariable<int> currentHatId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<int> currentWeaponId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<int> currentTopId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<int> currentBottomId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<int> currentAccessoryId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private GameObject spawnedHat;
	private GameObject spawnedWeapon;
	private SkinnedMeshRenderer spawnedTop;
	private SkinnedMeshRenderer spawnedBottom;
	private SkinnedMeshRenderer spawnedAccessory;

	public override void OnNetworkSpawn()
	{
		// 데이터가 변했을 때 실행될 콜백 등록
		currentHatId.OnValueChanged += (oldId, newId) => UpdateHatVisual(newId);
		currentWeaponId.OnValueChanged += (oldId, newId) => UpdateWeaponVisual(newId);
		currentTopId.OnValueChanged += (oldId, newId) => UpdateTopVisual(newId);
		currentBottomId.OnValueChanged += (oldId, newId) => UpdateBottomVisual(newId);
		currentAccessoryId.OnValueChanged += (oldId, newId) => UpdateAccessoryVisual(newId);

		RefreshAllVisuals();
	}

	private void RefreshAllVisuals()
	{
		UpdateHatVisual(currentHatId.Value);
		UpdateWeaponVisual(currentWeaponId.Value);
		UpdateTopVisual(currentTopId.Value);
		UpdateBottomVisual(currentBottomId.Value);
		UpdateAccessoryVisual(currentAccessoryId.Value);
	}

	public void EquipHat(int itemId) { if (IsOwner) RequestEquipServerRpc(0, itemId); }
	public void EquipWeapon(int itemId)
	{ 
		if (IsOwner) RequestEquipServerRpc(1, itemId);
		if (playerCombat != null)
		{
			playerCombat.SetWeaponTypeByItemId(itemId);
		}
	}
	public void EquipTop(int itemId) { if (IsOwner) RequestEquipServerRpc(2, itemId); }
	public void EquipBottom(int itemId) { if (IsOwner) RequestEquipServerRpc(3, itemId); }
	public void EquipAccessory(int itemId) { if (IsOwner) RequestEquipServerRpc(4, itemId); }

	public void UnequipHat()
	{
		if (IsOwner) RequestEquipServerRpc(0, -1);
	}

	public void UnequipWeapon()
	{
		if (IsOwner) RequestEquipServerRpc(1, -1);
		if (playerCombat != null)
		{
			playerCombat.SetWeaponTypeByItemId(-1);
		}
	}

	public void UnequipTop()
	{
		if (IsOwner) RequestEquipServerRpc(2, -1);
	}

	public void UnequipBottom()
	{
		if (IsOwner) RequestEquipServerRpc(3, -1);
	}

	public void UnequipAccessory()
	{
		if (IsOwner) RequestEquipServerRpc(4, -1);
	}

	public void UnequipAll()
	{
		if (!IsOwner) return;
		RequestEquipServerRpc(0, -1);
		RequestEquipServerRpc(1, -1);
		RequestEquipServerRpc(2, -1);
		RequestEquipServerRpc(3, -1);
		RequestEquipServerRpc(4, -1);
	}

	// ─── [ServerRpc] 하나의 RPC로 통합 관리 ─────────────────────────────

	[ServerRpc]
	private void RequestEquipServerRpc(int type, int itemId)
	{
		switch (type)
		{
			case 0: currentHatId.Value = itemId; break;
			case 1: currentWeaponId.Value = itemId; break;
			case 2: currentTopId.Value = itemId; break;
			case 3: currentBottomId.Value = itemId; break;
			case 4: currentAccessoryId.Value = itemId; break;
		}
	}

	// ─── [Visual Logic] 실제 오브젝트 생성 및 배치 ─────────────────────────────

	private void UpdateHatVisual(int itemId)
	{
		if (spawnedHat != null) Destroy(spawnedHat);
		if (itemId == -1) return;

		int index = FindIndex(itemId, hatItemIds);
		if (index >= 0)
		{
			spawnedHat = Instantiate(hatPrefabs[index], headSlot);
			ResetTransform(spawnedHat, hatPrefabs[index]);
		}
	}

	private void UpdateWeaponVisual(int itemId)
	{
		if (spawnedWeapon != null) Destroy(spawnedWeapon);
		if (itemId == -1) return;

		int index = FindIndex(itemId, weaponItemIds);
		if (index >= 0)
		{
			if (itemId >= 3088) spawnedWeapon = Instantiate(weaponPrefabs[index], bowSlot);
			else spawnedWeapon = Instantiate(weaponPrefabs[index], weaponSlot);
			ResetTransform(spawnedWeapon, weaponPrefabs[index]);
		}
	}

	private void UpdateTopVisual(int itemId)
	{
		if (spawnedTop != null) Destroy(spawnedTop.gameObject);
		if (itemId == -1) return;

		int index = FindIndex(itemId, topItemIds);
		if (index >= 0)
		{
			Transform parent = clothingParent != null ? clothingParent : transform;
			spawnedTop = Instantiate(topPrefabs[index], parent);
			spawnedTop.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			Rebind(spawnedTop);
		}
	}

	private void UpdateBottomVisual(int itemId)
	{
		if (spawnedBottom != null) Destroy(spawnedBottom.gameObject);
		if (itemId == -1) return;

		int index = FindIndex(itemId, bottomItemIds);
		if (index >= 0)
		{
			Transform parent = clothingParent != null ? clothingParent : transform;
			spawnedBottom = Instantiate(bottomPrefabs[index], parent);
			spawnedBottom.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			Rebind(spawnedBottom);
		}
	}

	private void UpdateAccessoryVisual(int itemId)
	{
		if (spawnedAccessory != null) Destroy(spawnedAccessory.gameObject);
		if (itemId == -1) return;

		int index = FindIndex(itemId, accessoryItemIds);
		if (index >= 0)
		{
			Transform parent = clothingParent != null ? clothingParent : transform;
			spawnedAccessory = Instantiate(accessoryPrefabs[index], parent);
			spawnedAccessory.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			Rebind(spawnedAccessory);
		}
	}

	// ─── [Helpers] 리바인딩 및 보조 도구 ─────────────────────────────

	private void Rebind(SkinnedMeshRenderer smr)
	{
		ClothingBoneData data = smr.GetComponent<ClothingBoneData>();
		if (data == null || targetSkeletonRoot == null) return;

		Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
		foreach (Transform bone in targetSkeletonRoot.GetComponentsInChildren<Transform>(true))
		{
			if (!boneMap.ContainsKey(bone.name)) boneMap.Add(bone.name, bone);
		}

		Transform[] mappedBones = new Transform[data.boneNames.Length];
		for (int i = 0; i < data.boneNames.Length; i++)
		{
			if (boneMap.TryGetValue(data.boneNames[i], out Transform matched))
				mappedBones[i] = matched;
		}

		smr.bones = mappedBones;

		if (!string.IsNullOrEmpty(data.rootBoneName) && boneMap.TryGetValue(data.rootBoneName, out Transform rb))
		{
			smr.rootBone = rb;
		}
		else if (boneMap.TryGetValue("pelvis_joint", out Transform pelvis))
		{
			smr.rootBone = pelvis;
		}
		smr.updateWhenOffscreen = true;
	}

	private int FindIndex(int id, int[] idArray)
	{
		for (int i = 0; i < idArray.Length; i++) if (idArray[i] == id) return i;
		return -1;
	}

	private void ResetTransform(GameObject obj, GameObject prefab)
	{
		obj.transform.localPosition = prefab.transform.localPosition;
		obj.transform.localRotation = prefab.transform.localRotation;
		obj.transform.localScale = prefab.transform.localScale;
	}
}