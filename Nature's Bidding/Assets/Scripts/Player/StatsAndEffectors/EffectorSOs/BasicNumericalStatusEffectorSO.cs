using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Basic Status Effector", menuName = "Stats/Status Effects/Basic Numerical")]
public class BasicNumericalStatusEffectorSO : StatusEffectorSO
{
    public StatType Stat;
    public float Value;
    public OperatorType OperationType;

    public override List<StatusEffect> GetStatusEffects()
    {
        return new List<StatusEffect> { new BasicNumericalStatusEffect
        {
            Stat = Stat,
            Value = Value,
            Duration = Duration,
            OperationType = OperationType
        }};
    }
}