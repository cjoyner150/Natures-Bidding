using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class TheEmperorTarotStatusEffect : StatusEffect
{
    const string emperorMultiplierId = "the_emperor_multiplier";
    public override StatsModifier GetStatsModifier() => null;

    public override void OnInitialize()
    {
        if (CheckMostGold())
        {
            AddEmperorEffector();
        }
    }

    private bool CheckMostGold()
    {
        float playerGold = Stats.GetStatByType(StatType.Gold);
        float mostGold = PersistentPlayerRegistry.Instance.GetAllPlayers().Max(p => p.gold);

        return playerGold >= mostGold;
    }

    private void AddEmperorEffector()
    {
        var effector = GameDataManager.Instance.GetEffector(emperorMultiplierId);
        
        StatusEffectManager.AddModifiers(effector);
    }

    public override void OnEnd()
    {
        StatusEffectManager.RemoveModifiers(emperorMultiplierId);
    }
}
