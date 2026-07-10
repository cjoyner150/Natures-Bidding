using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(ScriptedStatusEffectorSO))]
public class ScriptedStatusEffectorSOEditor : Editor
{
    private string[] _typeNames;
    private int _selectedIndex;

    private void OnEnable()
    {
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
        var paramsProp = serializedObject.FindProperty("_parameters");

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
            SyncParametersToType(typeProp.stringValue);
            serializedObject.Update();
        }

        if (!string.IsNullOrEmpty(typeProp.stringValue))
        {
            EditorGUILayout.HelpBox($"Effect: {typeProp.stringValue}", MessageType.Info);

            // Ensure parameters match current constructor (handles first load / type changes from elsewhere)
            var type = FindTypeByName(typeProp.stringValue);
            if (type != null)
            {
                var ctor = type.GetConstructors().FirstOrDefault();
                var ctorParams = ctor?.GetParameters() ?? new ParameterInfo[0];

                if (ctorParams.Length > 0)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

                    foreach (var p in ctorParams)
                    {
                        int idx = FindOrCreateParam(paramsProp, p.Name, p.ParameterType);
                        var element = paramsProp.GetArrayElementAtIndex(idx);

                        if (p.ParameterType == typeof(string))
                        {
                            var stringValueProp = element.FindPropertyRelative("StringValue");
                            EditorGUILayout.PropertyField(stringValueProp, new GUIContent(p.Name));
                        }
                        else
                        {
                            var valueProp = element.FindPropertyRelative("Value");
                            EditorGUILayout.PropertyField(valueProp, new GUIContent(p.Name));
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }

    private System.Type FindTypeByName(string name)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == name && typeof(StatusEffect).IsAssignableFrom(t));
    }

    private int FindOrCreateParam(SerializedProperty paramsProp, string paramName, System.Type paramType)
    {
        for (int i = 0; i < paramsProp.arraySize; i++)
        {
            var nameProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("ParamName");
            if (nameProp.stringValue == paramName) return i;
        }

        int newIndex = paramsProp.arraySize;
        paramsProp.InsertArrayElementAtIndex(newIndex);
        var newElement = paramsProp.GetArrayElementAtIndex(newIndex);
        newElement.FindPropertyRelative("ParamName").stringValue = paramName;
        newElement.FindPropertyRelative("Value").floatValue = 0f;
        newElement.FindPropertyRelative("StringValue").stringValue = "";
        return newIndex;
    }

    private void SyncParametersToType(string typeName)
    {
        var type = FindTypeByName(typeName);
        if (type == null) return;

        var paramsProp = serializedObject.FindProperty("_parameters");
        paramsProp.ClearArray();

        var ctor = type.GetConstructors().FirstOrDefault();
        if (ctor == null) return;

        foreach (var p in ctor.GetParameters())
        {
            int idx = paramsProp.arraySize;
            paramsProp.InsertArrayElementAtIndex(idx);
            var elem = paramsProp.GetArrayElementAtIndex(idx);
            elem.FindPropertyRelative("ParamName").stringValue = p.Name;
            elem.FindPropertyRelative("Value").floatValue = 0f;
            elem.FindPropertyRelative("StringValue").stringValue = "";
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif