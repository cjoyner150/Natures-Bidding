using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Basic Status Effector", menuName = "Stats/Status Effects/Basic")]
public class BasicStatusEffectorSO : StatusEffectorSO
{
    public StatType Stat;
    public float Value;
    public OperatorType OperationType;

    public override List<StatusEffect> GetStatusEffects()
    {
        return new List<StatusEffect> { new BasicStatusEffect
        {
            Stat = Stat,
            Value = Value,
            Duration = Duration,
            OperationType = OperationType
        }};
    }
}