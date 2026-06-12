using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CanvasGroup))]
[CanEditMultipleObjects]
public class CanvasGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CanvasGroup group = (CanvasGroup)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Show (ON)"))
        {
            Undo.RecordObject(group, "Show CanvasGroup");
            SetGroup(group, 1f, true, true);
        }

        if (GUILayout.Button("Hide (OFF)"))
        {
            Undo.RecordObject(group, "Hide CanvasGroup");
            SetGroup(group, 0f, false, false);
        }

        EditorGUILayout.EndHorizontal();
    }
    
    private void SetGroup(CanvasGroup group, float alpha, bool interactable, bool blocksRaycasts)
    {
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = blocksRaycasts;
        
        EditorUtility.SetDirty(group);
    }
}