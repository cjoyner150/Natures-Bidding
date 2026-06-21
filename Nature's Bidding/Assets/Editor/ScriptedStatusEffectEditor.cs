using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(ScriptedStatusEffectorSO))]
public class ScriptedStatusEffectorSOEditor : Editor
{
    private string[] _typeNames;
    private int _selectedIndex;

    private void OnEnable()
    {
        // Find all concrete StatusEffect subclasses
        _typeNames = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(StatusEffect).IsAssignableFrom(t) &&
                        !t.IsAbstract &&
                        t != typeof(StatusEffect))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        var so = (ScriptedStatusEffectorSO)target;
        var typeProp = serializedObject.FindProperty("_effectTypeName");
        _selectedIndex = System.Array.IndexOf(_typeNames, typeProp.stringValue);
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        var typeProp = serializedObject.FindProperty("_effectTypeName");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effect Type", EditorStyles.boldLabel);

        if (_typeNames.Length == 0)
        {
            EditorGUILayout.HelpBox("No StatusEffect subclasses found.", MessageType.Warning);
            return;
        }

        int newIndex = EditorGUILayout.Popup("Select Effect", _selectedIndex, _typeNames);
        if (newIndex != _selectedIndex)
        {
            _selectedIndex = newIndex;
            typeProp.stringValue = _typeNames[_selectedIndex];
            serializedObject.ApplyModifiedProperties();
        }

        if (!string.IsNullOrEmpty(typeProp.stringValue))
            EditorGUILayout.HelpBox($"Effect: {typeProp.stringValue}", MessageType.Info);
    }
}
#endif
