#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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

        if (string.IsNullOrEmpty(typeProp.stringValue)) return;

        EditorGUILayout.HelpBox($"Effect: {typeProp.stringValue}", MessageType.Info);

        var type = FindTypeByName(typeProp.stringValue);
        if (type == null) return;

        var ctor = type.GetConstructors().FirstOrDefault();
        var ctorParams = ctor?.GetParameters() ?? new ParameterInfo[0];

        bool needsSync = paramsProp.arraySize != ctorParams.Length;
        if (!needsSync)
        {
            for (int i = 0; i < ctorParams.Length; i++)
            {
                var nameProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("ParamName");
                if (nameProp.stringValue != ctorParams[i].Name)
                {
                    needsSync = true;
                    break;
                }
            }
        }

        if (needsSync)
        {
            SyncParametersToType(typeProp.stringValue);
            serializedObject.Update();
        }

        if (ctorParams.Length == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

        foreach (var p in ctorParams)
        {
            int idx = FindOrCreateParam(paramsProp, p.Name);
            var element = paramsProp.GetArrayElementAtIndex(idx);
            DrawParameterField(element, p);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawParameterField(SerializedProperty element, ParameterInfo p)
    {
        var paramType = p.ParameterType;

        if (paramType.IsEnum)
        {
            var stringProp = element.FindPropertyRelative("StringValue");
            var currentEnumValue = string.IsNullOrEmpty(stringProp.stringValue)
                ? (Enum)Activator.CreateInstance(paramType)
                : (Enum)Enum.Parse(paramType, stringProp.stringValue);

            var newEnumValue = EditorGUILayout.EnumPopup(p.Name, currentEnumValue);
            stringProp.stringValue = newEnumValue.ToString();
        }
        else if (paramType == typeof(bool))
        {
            var boolProp = element.FindPropertyRelative("BoolValue");
            EditorGUILayout.PropertyField(boolProp, new GUIContent(p.Name));
        }
        else if (paramType == typeof(string))
        {
            var stringProp = element.FindPropertyRelative("StringValue");
            EditorGUILayout.PropertyField(stringProp, new GUIContent(p.Name));
        }
        else if (paramType == typeof(float) || paramType == typeof(int))
        {
            var floatProp = element.FindPropertyRelative("FloatValue");
            EditorGUILayout.PropertyField(floatProp, new GUIContent(p.Name));
        }
        else if (typeof(IEnumerable<StatusEffectorSO>).IsAssignableFrom(paramType) ||
                 paramType == typeof(List<StatusEffectorSO>))
        {
            var listProp = element.FindPropertyRelative("ObjectListValue");
            EditorGUILayout.PropertyField(listProp, new GUIContent(p.Name), true);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"No editor support for parameter type '{paramType.Name}' ('{p.Name}'). Add a case in ScriptedStatusEffectorSOEditor.DrawParameterField.",
                MessageType.Warning
            );
        }
    }

    private System.Type FindTypeByName(string name)
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == name && typeof(StatusEffect).IsAssignableFrom(t));
    }

    private int FindOrCreateParam(SerializedProperty paramsProp, string paramName)
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
        return newIndex;
    }

    private void SyncParametersToType(string typeName)
    {
        var type = FindTypeByName(typeName);
        if (type == null) return;

        var paramsProp = serializedObject.FindProperty("_parameters");
        var ctor = type.GetConstructors().FirstOrDefault();
        var ctorParams = ctor?.GetParameters() ?? new ParameterInfo[0];
        var expectedNames = ctorParams.Select(p => p.Name).ToHashSet();

        for (int i = paramsProp.arraySize - 1; i >= 0; i--)
        {
            var nameProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("ParamName");
            if (!expectedNames.Contains(nameProp.stringValue))
            {
                paramsProp.DeleteArrayElementAtIndex(i);
            }
        }

        foreach (var p in ctorParams)
        {
            bool exists = false;
            for (int i = 0; i < paramsProp.arraySize; i++)
            {
                var nameProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("ParamName");
                if (nameProp.stringValue == p.Name)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                int idx = paramsProp.arraySize;
                paramsProp.InsertArrayElementAtIndex(idx);
                var elem = paramsProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("ParamName").stringValue = p.Name;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif