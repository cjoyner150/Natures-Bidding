using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class EffectorData : IDisposable
{
    public string Id;
    public List<StatusEffect> Effects;
    public List<StatsModifier> StatsModifiers = new List<StatsModifier>();

    public EffectorData(StatusEffectorSO so, Stats playerStats, PlayerStatusEffectManager statusManager, StatsMediator statsMediator) 
    {
        Effects = so.GetStatusEffects();
        Id = so.Id;

        foreach (var effect in Effects)
        {
            effect.Initialize(playerStats, statusManager);

            var mod = effect.GetStatsModifier();

            if (mod != null)
            {
                StatsModifiers.Add(mod);
                statsMediator.AddModifier(mod);
            }
        }
    }

    public void OnTick(float delta)
    {
        foreach (var effect in Effects)
        {
            effect.OnTick(delta);
        }
    }

    public void Dispose()
    {
        foreach (var effect in Effects)
        {
            effect.OnEnd();
        }

        foreach (var modifier in StatsModifiers)
        {
            modifier.Dispose();
        }
    }
}
