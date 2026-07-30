using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class TheWheelTarotStatusEffect : StatusEffect
{
    int amount;
    List<StatusEffectorSO> effectors = new List<StatusEffectorSO>();
    public override StatsModifier GetStatsModifier() => null;

    public TheWheelTarotStatusEffect(int amount)
    {
        this.amount = amount;
    }

    public override void OnInitialize()
    {
        AddRandomEffectors();
    }

    private void AddRandomEffectors()
    {

        for (var i = 0; i < amount; i++)
        {
            effectors.Add(GameDataManager.Instance.GetRandomEffector());
        }
        
        StatusEffectManager.AddModifiers(effectors);
    }

    public override void OnEnd()
    {
        StatusEffectManager.RemoveModifiers(effectors.Select(e => e.Id));
    }
}
