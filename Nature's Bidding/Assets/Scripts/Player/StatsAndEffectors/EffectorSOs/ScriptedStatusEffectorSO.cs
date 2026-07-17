using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New Scripted Status Effect", menuName = "Stats/Status Effects/Scripted Status Effect")]
public class ScriptedStatusEffectorSO : StatusEffectorSO
{
    [HideInInspector][SerializeField] private string _effectTypeName;

    [Serializable]
    public class EffectParameter
    {
        public string ParamName;
        public float Value;
        public string StringValue;
    }

    [HideInInspector][SerializeField] private List<EffectParameter> _parameters = new();

    public string EffectTypeName => _effectTypeName;
    public List<EffectParameter> Parameters => _parameters;

    public override List<StatusEffect> GetStatusEffects()
    {
        if (string.IsNullOrEmpty(_effectTypeName))
        {
            Debug.LogError($"[{name}] No effect type name set.");
            return new List<StatusEffect>();
        }

        var type = Type.GetType(_effectTypeName);
        if (type == null)
        {
            Debug.LogError($"[{name}] Could not find type '{_effectTypeName}'.");
            return new List<StatusEffect>();
        }

        if (!typeof(StatusEffect).IsAssignableFrom(type))
        {
            Debug.LogError($"[{name}] Type '{_effectTypeName}' is not a StatusEffect.");
            return new List<StatusEffect>();
        }

        var constructor = type.GetConstructors().FirstOrDefault();
        StatusEffect effect;

        if (constructor == null || constructor.GetParameters().Length == 0)
        {
            effect = (StatusEffect)Activator.CreateInstance(type);
        }
        else
        {
            var paramInfos = constructor.GetParameters();
            object[] args = new object[paramInfos.Length];

            for (int i = 0; i < paramInfos.Length; i++)
            {
                var match = _parameters.FirstOrDefault(p => p.ParamName == paramInfos[i].Name);
                if (match == null)
                {
                    Debug.LogError($"[{name}] Missing parameter '{paramInfos[i].Name}' for {type.Name}.");
                    args[i] = GetDefault(paramInfos[i].ParameterType);
                    continue;
                }
                args[i] = paramInfos[i].ParameterType == typeof(string)
                    ? (object)match.StringValue
                    : Convert.ChangeType(match.Value, paramInfos[i].ParameterType);
            }

            effect = (StatusEffect)Activator.CreateInstance(type, args);
        }

        return new List<StatusEffect> { effect };
    }

    private object GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
}