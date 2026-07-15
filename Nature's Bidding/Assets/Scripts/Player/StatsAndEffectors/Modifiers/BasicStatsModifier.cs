using System;

public class BasicStatsModifier : StatsModifier
{
    StatType statType;
    Func<float, float> operation;

    public BasicStatsModifier(StatType type, float duration, Func<float, float> op) : base(duration)
    {
        this.statType = type;
        this.operation = op;
    }

    public override void Handle(object sender, Query query)
    {
        if (query.StatType == statType)
        {
            query.Value = operation(query.Value);
        }
    }
}