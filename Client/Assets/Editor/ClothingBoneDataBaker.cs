using UnityEditor;
using UnityEngine;

public class ClothingBoneDataBaker
{
	[MenuItem("Tools/Equipment/Bake Bone Names From Selected")]
	private static void Bake()
	{
		var selected = Selection.activeGameObject;

		if (selected == null)
		{
			Debug.LogError("선택된 오브젝트가 없습니다.");
			return;
		}

		var smr = selected.GetComponent<SkinnedMeshRenderer>();

		if (smr == null)
		{
			Debug.LogError("선택한 오브젝트에 SkinnedMeshRenderer가 없습니다.");
			return;
		}

		if (smr.bones == null || smr.bones.Length == 0)
		{
			Debug.LogError($"{smr.name}: bones 배열이 비어 있습니다.");
			return;
		}

		int nullCount = 0;
		foreach (var bone in smr.bones)
		{
			if (bone == null)
				nullCount++;
		}

		if (nullCount > 0)
		{
			Debug.LogError($"{smr.name}: bones 중 null이 {nullCount}개 있습니다. 정상 원본 옷에서 Bake해야 합니다.");
			return;
		}

		var data = smr.GetComponent<ClothingBoneData>();
		if (data == null)
			data = smr.gameObject.AddComponent<ClothingBoneData>();

		data.rootBoneName = smr.rootBone != null ? smr.rootBone.name : "pelvis_joint";
		data.boneNames = new string[smr.bones.Length];

		for (int i = 0; i < smr.bones.Length; i++)
		{
			data.boneNames[i] = smr.bones[i].name;
		}

		EditorUtility.SetDirty(data);
		EditorUtility.SetDirty(smr.gameObject);

		Debug.Log($"Bone 이름 저장 완료: {smr.name}, Count: {data.boneNames.Length}");
	}
}