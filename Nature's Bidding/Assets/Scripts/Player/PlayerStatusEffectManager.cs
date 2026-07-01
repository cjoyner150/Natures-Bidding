using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityUtils;

public class PlayerStatusEffectManager : MonoBehaviour
{
    [SerializeField] List<StatusEffectorSO> _debugShowCurrentStatusEffectors = new();
    [SerializeField] List<StatusEffectorSO> _debugAddStatusEffectors = new();

    public Action OnInitializeCompleted;

    List<EffectorData> activeEffectors = new();

    private PlayerHealth playerHealth;
    private StatsMediator statsMediator;
    private Stats playerStats;
    
    public void Initialize(Stats stats, ulong clientId)
    {
        IEnumerable<StatusEffectorSO> StatusEffectors = GetStatusEffectors(clientId);
        playerHealth = GetComponent<PlayerHealth>();

        statsMediator = stats.Mediator;
        playerStats = stats;

        AddModifiers(StatusEffectors);
        OnInitializeCompleted?.Invoke();
    }

    IEnumerable<StatusEffectorSO> GetStatusEffectors(ulong clientId)
    {
        PersistentPlayerData data = PersistentPlayerRegistry.Instance.GetByClientId(clientId);

        var effectors = data.GetArtifactEffectors()
            .Concat(data.GetMaskEffectors())
            .Concat(data.GetTarotEffectors());

        return effectors;
    }

    public void AddModifiers(IEnumerable<StatusEffectorSO> addedEffects)
    {
        Debug.Log($"Added modifiers: {string.Join(", ", addedEffects.Select(x => x.Id))}");
        _debugShowCurrentStatusEffectors.AddRange(addedEffects);

        foreach (var effector in addedEffects)
        {
            var effectData = new EffectorData(effector, playerStats, this, statsMediator);
            activeEffectors.Add(effectData);
        }

        playerHealth.SendMaxHealthToServerRpc(playerStats.MaxHealth, playerHealth.OwnerClientId);
    }

    public void RemoveModifiers(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            var effectData = activeEffectors.Find(e => e.Id == id);

            if (effectData != null)
            {
                effectData.Dispose();
                activeEffectors.Remove(effectData);
            }

            var debugEffect = _debugShowCurrentStatusEffectors.Find(e => e.Id == id);
            if (debugEffect != null)
            {
                _debugShowCurrentStatusEffectors.Remove(debugEffect);
            }
        }
    }

    [ContextMenu("Add Debug Modifiers")]
    public void DebugAddModifiers()
    {
        AddModifiers(_debugAddStatusEffectors);
    }

    private void Update()
    {
        statsMediator?.Update(Time.deltaTime);

        foreach (var effect in activeEffectors)
        {
            effect.OnTick(Time.deltaTime);
        }
        

        print("[Player Stats] Modifiers Initialized. "+playerStats?.ToString());
    }

}
