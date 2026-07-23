using System;

[System.Serializable]
public class BasicNumericalStatusEffect : StatusEffect
{
    public OperatorType OperationType;

    public override StatsModifier GetStatsModifier()
    {
        return new BasicStatsModifier(Stat, Duration, GetFuncByOperation(OperationType, Value));
    }
}

