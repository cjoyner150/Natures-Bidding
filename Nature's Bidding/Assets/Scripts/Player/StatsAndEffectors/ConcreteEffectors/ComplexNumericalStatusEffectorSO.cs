using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Complex Status Effector", menuName = "Stats/Status Effects/Complex Numerical")]
public class ComplexNumericalStatusEffectorSO : StatusEffectorSO
{
    public StatType Stat;
    public OperatorType TargetOperationType;
    public StatType EffectByStat;
    public OperatorType ChangeEffectByOperation;
    public float ChangeEffectByOperationValue;

    public override List<StatusEffect> GetStatusEffects()
    {
        return new List<StatusEffect> { new ComplexNumericalStatusEffect
        {
            Stat = Stat,
            Value = ChangeEffectByOperationValue,
            Duration = Duration,
            TargetOperationType = TargetOperationType,
            EffectByStatType = EffectByStat,
            ChangeEffectByOperation = ChangeEffectByOperation
        }};
    }
}