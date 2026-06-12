#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

public class TMPFontReplacer : EditorWindow
{
    private TMP_FontAsset _targetFont;

    [MenuItem("Tools/TMP 폰트 일괄 교체")]
    private static void Open() => GetWindow<TMPFontReplacer>("TMP 폰트 교체");

    private void OnGUI()
    {
        GUILayout.Label("교체할 폰트 선택", EditorStyles.boldLabel);
        _targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font", _targetFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        GUI.enabled = _targetFont != null;

        if (GUILayout.Button("씬 + 프리팹 전체 교체"))
            ReplaceAll();

        GUI.enabled = true;
    }

    private void ReplaceAll()
    {
        int count = 0;

        // 현재 씬
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == _targetFont) continue;
            Undo.RecordObject(t, "Replace TMP Font");
            t.font = _targetFont;
            EditorUtility.SetDirty(t);
            count++;
        }

        // 프리팹
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            // 미싱 스크립트가 있는 프리팹은 저장 시 오류 → 건너뜀
            if (HasMissingScripts(go)) continue;

            bool dirty = false;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.font == _targetFont) continue;
                t.font = _targetFont;
                dirty  = true;
                count++;
            }
            if (dirty) EditorUtility.SetDirty(go);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TMPFontReplacer] {count}개 교체 완료 → {_targetFont.name}");
    }

    private static bool HasMissingScripts(GameObject go)
    {
        foreach (var comp in go.GetComponentsInChildren<Component>(true))
            if (comp == null) return true;
        return false;
    }
}
#endif
