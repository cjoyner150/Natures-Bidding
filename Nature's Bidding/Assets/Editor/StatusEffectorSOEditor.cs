#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(StatusEffectorSO), true)]
public class StatusEffectorSOEditor : Editor
{
    private bool _isDuplicate = false;
    private string _duplicateAssetPath = null;

    private void OnEnable()
    {
        CheckForDuplicates();
    }

    private void CheckForDuplicates()
    {
        var so = (StatusEffectorSO)target;
        if (string.IsNullOrEmpty(so.Id)) return;

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(StatusEffectorSO).Name}");

        _isDuplicate = false;
        _duplicateAssetPath = null;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var other = AssetDatabase.LoadAssetAtPath<StatusEffectorSO>(path);

            if (other == null || other == target) continue;

            if (other.Id == so.Id)
            {
                _isDuplicate = true;
                _duplicateAssetPath = path;
                break;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        var so = (StatusEffectorSO)target;

        if (string.IsNullOrEmpty(so.Id))
        {
            EditorGUILayout.HelpBox("This effector has no ID!", MessageType.Error);
            if (GUILayout.Button("Generate ID from asset name"))
            {
                var serialized = new SerializedObject(target);
                serialized.FindProperty("_id").stringValue =
                    target.name.ToLower().Replace(" ", "_");
                serialized.ApplyModifiedProperties();
                CheckForDuplicates();
            }
        }
        else if (_isDuplicate)
        {
            EditorGUILayout.HelpBox(
                $"Duplicate ID '{so.Id}' also used by:\n{_duplicateAssetPath}",
                MessageType.Error
            );
        }
        else
        {
            EditorGUILayout.HelpBox($"ID: {so.Id}", MessageType.Info);
        }

        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            CheckForDuplicates();
        }
    }
}
#endif