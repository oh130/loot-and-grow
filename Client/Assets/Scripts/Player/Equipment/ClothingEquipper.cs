using System.Collections.Generic;
using UnityEngine;

public class ClothingEquipper : MonoBehaviour
{
	[Header("Top Clothing Prefabs")]
	[SerializeField] private SkinnedMeshRenderer[] clothingPrefabs = new SkinnedMeshRenderer[4];

	[Header("Player Skeleton")]
	[SerializeField] private Transform targetSkeletonRoot;

	[Header("Attach Parent")]
	[SerializeField] private Transform clothingParent;

	private SkinnedMeshRenderer currentClothing;

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Alpha5)) EquipTop(0);
		if (Input.GetKeyDown(KeyCode.Alpha6)) EquipTop(1);
		if (Input.GetKeyDown(KeyCode.Alpha7)) EquipTop(2);
		if (Input.GetKeyDown(KeyCode.Alpha8)) EquipTop(3);
	}

	public void EquipTop(int index)
	{
		if (index < 0 || index >= clothingPrefabs.Length)
		{
			Debug.LogError($"잘못된 옷 인덱스: {index}");
			return;
		}

		SkinnedMeshRenderer prefab = clothingPrefabs[index];

		if (prefab == null)
		{
			Debug.LogError($"clothingPrefabs[{index}]가 비어 있습니다.");
			return;
		}

		if (targetSkeletonRoot == null)
		{
			Debug.LogError("targetSkeletonRoot가 비어 있습니다.");
			return;
		}

		if (currentClothing != null)
		{
			Destroy(currentClothing.gameObject);
		}

		Transform parent = clothingParent != null ? clothingParent : transform;

		currentClothing = Instantiate(prefab, parent);
		currentClothing.transform.localPosition = Vector3.zero;
		currentClothing.transform.localRotation = Quaternion.identity;
		currentClothing.transform.localScale = Vector3.one;

		Rebind(currentClothing);

		Debug.Log($"상의 장착 완료: {currentClothing.name}");
	}

	private void Rebind(SkinnedMeshRenderer smr)
	{
		ClothingBoneData data = smr.GetComponent<ClothingBoneData>();

		if (data == null)
		{
			Debug.LogError($"{smr.name}에 ClothingBoneData가 없습니다.");
			return;
		}

		if (data.boneNames == null || data.boneNames.Length == 0)
		{
			Debug.LogError($"{smr.name}의 boneNames가 비어 있습니다.");
			return;
		}

		Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

		foreach (Transform bone in targetSkeletonRoot.GetComponentsInChildren<Transform>(true))
		{
			if (!boneMap.ContainsKey(bone.name))
				boneMap.Add(bone.name, bone);
		}

		Transform[] mappedBones = new Transform[data.boneNames.Length];

		for (int i = 0; i < data.boneNames.Length; i++)
		{
			string boneName = data.boneNames[i];

			if (!string.IsNullOrEmpty(boneName) &&
				boneMap.TryGetValue(boneName, out Transform matchedBone))
			{
				mappedBones[i] = matchedBone;
			}
			else
			{
				Debug.LogWarning($"플레이어 skeleton에서 bone을 찾지 못함: {boneName}");
			}
		}

		smr.bones = mappedBones;

		if (!string.IsNullOrEmpty(data.rootBoneName) &&
			boneMap.TryGetValue(data.rootBoneName, out Transform rootBone))
		{
			smr.rootBone = rootBone;
		}
		else if (boneMap.TryGetValue("pelvis_joint", out Transform pelvis))
		{
			smr.rootBone = pelvis;
		}

		smr.updateWhenOffscreen = true;
		smr.enabled = true;
		smr.gameObject.SetActive(true);
	}
}