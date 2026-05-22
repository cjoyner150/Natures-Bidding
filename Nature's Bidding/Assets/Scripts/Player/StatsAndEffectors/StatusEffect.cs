using System;

[System.Serializable]
public abstract class StatusEffect
{
    public string Name;
    public StatType Stat;
    public float Value;
    public float Duration;
    public abstract StatsModifier GetModifier();
}

[System.Serializable]
public class BasicStatusEffect : StatusEffect
{
    public OperatorType OperationType;

    public override StatsModifier GetModifier()
    {
        var modifier = OperationType == OperatorType.Multiplication ? new BasicStatsModifier(Stat, Duration, x => x * Value)
            : OperationType == OperatorType.Addition ? new BasicStatsModifier(Stat, Duration, x => x + Value)
            : OperationType == OperatorType.Division ? new BasicStatsModifier(Stat, Duration, x => x / Value)
            : OperationType == OperatorType.Subtraction ? new BasicStatsModifier(Stat, Duration, x => x - Value)
            : throw new Exception("Basic Operation should only be of a base operation type");

        return modifier;
    }
}


