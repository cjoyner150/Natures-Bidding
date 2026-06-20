using System;

public class ComplexNumericalStatusEffect : StatusEffect
{
    public OperatorType TargetOperationType;
    public StatType EffectByStatType;
    public OperatorType ChangeEffectByOperation;

    public override StatsModifier GetStatsModifier()
    {
        var modifier = new BasicStatsModifier(Stat, Duration, currentTargetStatValue =>
        {
            float calculatedModifier = GetFuncByOperation(ChangeEffectByOperation, Value)(Stats.GetStatByType(EffectByStatType));

            return GetFuncByOperation(TargetOperationType, calculatedModifier)(currentTargetStatValue);
        });

        return modifier;
    }
}

