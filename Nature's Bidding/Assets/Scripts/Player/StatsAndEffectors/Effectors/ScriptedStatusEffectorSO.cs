using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "New Scripted Status Effect", menuName = "Stats/Status Effects/Scripted Status Effect")]
public class ScriptedStatusEffectorSO : StatusEffectorSO
{
    [HideInInspector] [SerializeField] private string _effectTypeName;

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

        var effect = (StatusEffect)Activator.CreateInstance(type);
        return new List<StatusEffect> { effect };
    }
}
