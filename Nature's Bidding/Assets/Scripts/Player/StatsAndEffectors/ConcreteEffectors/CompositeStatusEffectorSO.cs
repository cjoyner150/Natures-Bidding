using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "New Composite Status Effector", menuName = "Stats/Status Effects/Composite Effect")]
public class CompositeStatusEffectorSO : StatusEffectorSO
{
    public List<StatusEffectorSO> Effects;

    public override List<StatusEffect> GetStatusEffects()
    {
        var effects = new List<StatusEffect>();
        foreach (var effect in Effects)
            if (effect != null)
                effects.AddRange(effect.GetStatusEffects());
        return effects;
    }
}
