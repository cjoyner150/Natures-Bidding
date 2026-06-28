using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityUtils;

public class PlayerStatusEffectManager : MonoBehaviour
{
    [SerializeField] List<StatusEffectorSO> _debugShowCurrentStatusEffectors = new();
    [SerializeField] List<StatusEffectorSO> _debugAddStatusEffectors = new();

    List<StatusEffect> activeEffects = new();

    private PlayerHealth playerHealth;
    private StatsMediator statsMediator;
    private Stats playerStats;
    
    public void Initialize(Stats stats, ulong clientId)
    {
        List<StatusEffectorSO> StatusEffectors = GetStatusEffectors(clientId);
        playerHealth = GetComponent<PlayerHealth>();

        statsMediator = stats.Mediator;
        playerStats = stats;

        AddModifiers(StatusEffectors);
    }

    List<StatusEffectorSO> GetStatusEffectors(ulong clientId)
    {
        PersistentPlayerData data = PersistentPlayerRegistry.Instance.GetByClientId(clientId);

        List<StatusEffectorSO> effectors = new();

        effectors = data.GetArtifactEffectors()
            .Concat(data.GetMaskEffectors())
            .Concat(data.GetTarotEffectors())
            .ToList();

        return effectors;
    }

    public void AddModifiers(List<StatusEffectorSO> addedEffects)
    {
        Debug.Log($"Added modifiers: {string.Join(", ", addedEffects.Select(x => x.Id))}");
        _debugShowCurrentStatusEffectors.AddRange(addedEffects);

        foreach (var effector in addedEffects)
        {
            foreach (var effect in effector.GetStatusEffects())
            {
                effect.Initialize(playerStats, this);
                activeEffects.Add(effect);

                var modifier = effect.GetStatsModifier();

                if (modifier != null) statsMediator?.AddModifier(modifier);
            }
        }

        playerHealth.SendMaxHealthToServerRpc(playerStats.MaxHealth, playerHealth.OwnerClientId);
    }

    [ContextMenu("Add Debug Modifiers")]
    public void DebugAddModifiers()
    {
        AddModifiers(_debugAddStatusEffectors);
    }

    private void Update()
    {
        statsMediator?.Update(Time.deltaTime);

        foreach (var effect in activeEffects)
        {
            effect.OnTick(Time.deltaTime);
        }
        

        print("[Player Stats] Modifiers Initialized. "+playerStats?.ToString());
    }

}
