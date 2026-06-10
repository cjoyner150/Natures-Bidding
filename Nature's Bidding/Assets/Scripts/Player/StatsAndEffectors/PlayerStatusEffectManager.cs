using System.Collections.Generic;
using UnityEngine;
using UnityUtils;

public class PlayerStatusEffectManager : MonoBehaviour
{
    [SerializeField] List<StatusEffectorSO> _debugStatusEffectors = new();

    List<StatusEffectorSO> StatusEffectors = new();

    private StatsMediator statsMediator;
    private Stats playerStats;
    
    public void Initialize(Stats stats, List<StatusEffectorSO> effectorSOs)
    {
        StatusEffectors = effectorSOs;
        _debugStatusEffectors = StatusEffectors;

        statsMediator = stats.Mediator;

        playerStats = stats;

        AddModifiers();
    }

    void AddModifiers()
    {
        foreach (var effector in StatusEffectors)
        {
            foreach (var effect in effector.GetStatusEffects())
            {
                effect.Initialize(playerStats);

                var modifier = effect.GetStatsModifier();

                if (modifier != null) statsMediator?.AddModifier(modifier);
            }
        }
    }

    private void Update()
    {
        statsMediator?.Update(Time.deltaTime);

        foreach (var effector in StatusEffectors)
        {
            foreach (var effect in effector.GetStatusEffects())
            {
                effect.OnTick(Time.deltaTime);
            }
        }

        print("[Player Stats] Modifiers Initialized. "+playerStats?.ToString());
    }

}
