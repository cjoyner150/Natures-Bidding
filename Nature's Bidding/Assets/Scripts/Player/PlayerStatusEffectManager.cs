using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
    private PlayerContext ctx;

    Vector3 initialLocalScale;
    
    public void Initialize(PlayerContext ctx, Stats stats, ulong clientId)
    {
        this.ctx = ctx;

        IEnumerable<StatusEffectorSO> StatusEffectors = GetStatusEffectors(clientId);
        playerHealth = GetComponent<PlayerHealth>();

        statsMediator = stats.Mediator;
        playerStats = stats;
        initialLocalScale = transform.localScale;

        AddModifiers(StatusEffectors);

        PlayerCombatHooks.OnItemAdded += OnItemAdded;

        OnInitializeCompleted?.Invoke();

    }

    IEnumerable<StatusEffectorSO> GetStatusEffectors(ulong clientId)
    {
        PlayerData data = PersistentPlayerRegistry.Instance.GetByClientId(clientId);

        var effectors = data.GetArtifactEffectors()
            .Concat(data.GetMaskEffectors())
            .Concat(data.GetTarotEffectors());

        return effectors;
    }

    public void AddModifiers(IEnumerable<StatusEffectorSO> addedEffects)
    {
        GameLogger.Log(LogSeverity.Debug, $"Added modifiers: {string.Join(", ", addedEffects.Select(x => x.Id))}");
        _debugShowCurrentStatusEffectors.AddRange(addedEffects);

        foreach (var effector in addedEffects)
        {
            var effectData = new EffectorData(effector, playerStats, this, statsMediator);
            activeEffectors.Add(effectData);
        }

        UpdateStatValues();
    }

    public void AddModifiers(StatusEffectorSO addedEffect)
    {
        GameLogger.Log(LogSeverity.Debug, $"Added modifiers: {addedEffect.Id}");
        _debugShowCurrentStatusEffectors.Add(addedEffect);

        var effectData = new EffectorData(addedEffect, playerStats, this, statsMediator);
        activeEffectors.Add(effectData);

        UpdateStatValues();
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

        UpdateStatValues();
    }

    public void RemoveModifiers(string id)
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

        UpdateStatValues();
    }

    private void UpdateStatValues() 
    {
        playerHealth.SendMaxHealthToServerRpc(playerStats.MaxHealth, playerHealth.OwnerClientId);
        transform.localScale = initialLocalScale * playerStats.Size;
        ctx.maxJumps = ctx.playerStats.Jumps;
    }

    private void OnItemAdded(string itemId)
    {
        var effector = GameDataManager.Instance.GetEffector(itemId);
        AddModifiers(effector);
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

    private void OnDestroy()
    {
        PlayerCombatHooks.OnItemAdded -= OnItemAdded;
    }

}
