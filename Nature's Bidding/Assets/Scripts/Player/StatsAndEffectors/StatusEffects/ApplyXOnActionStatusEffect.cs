using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class ApplyXOnActionStatusEffect : StatusEffect
{
    protected ulong selfClientId;
    protected bool applyToSelf;
    protected List<StatusEffectorSO> effects;
    protected ApplyEffectOnActionType actionType;
    public override StatsModifier GetStatsModifier() => null;

    public ApplyXOnActionStatusEffect(ApplyEffectOnActionType actionType, List<StatusEffectorSO> effectsToApply, bool applyToSelf = true)
    {
        this.actionType = actionType;
        this.applyToSelf = applyToSelf;
        effects = effectsToApply;
    }

    public override async void OnInitialize()
    {
        Debug.Log($"[ApplyXOnActionStatusEffect] OnInitialize called. Instance hash: {GetHashCode()}, actionType: {actionType}");

        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton.ConnectedClientsList.All(x => x.PlayerObject != null)
        );

        selfClientId = NetworkManager.Singleton.LocalClientId;

        InitHooks();
        Debug.Log($"[ApplyXOnActionStatusEffect] Hooks initialized. Instance hash: {GetHashCode()}");
    }

    public override void OnEnd()
    {
        RemoveHooks();
    }

    protected virtual void OnApplyEffectTo(ulong targetId) { }

    protected void InitHooks()
    {

        switch (actionType) {
            case ApplyEffectOnActionType.OnParry:
                PlayerCombatHooks.OnParry += ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnAttack:
                PlayerCombatHooks.OnAttack += ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnHit:
                PlayerCombatHooks.OnHit += ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnDeath:
                PlayerCombatHooks.OnDeath += ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnKill:
                PlayerCombatHooks.OnKill += ApplyEffectsToPlayer;
                break;
        }
    }

    protected void RemoveHooks()
    {
        switch (actionType)
        {
            case ApplyEffectOnActionType.OnParry:
                PlayerCombatHooks.OnParry -= ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnAttack:
                PlayerCombatHooks.OnAttack -= ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnHit:
                PlayerCombatHooks.OnHit -= ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnDeath:
                PlayerCombatHooks.OnDeath -= ApplyEffectsToPlayer;
                break;
            case ApplyEffectOnActionType.OnKill:
                PlayerCombatHooks.OnKill -= ApplyEffectsToPlayer;
                break;
        }
    }

    protected void ApplyEffectsToPlayer(ulong targetClientId)
    {
        ulong applyToPlayerId = applyToSelf ? selfClientId : targetClientId;

        string[] effectIds = effects.Select(x => x.Id).ToArray();
        Debug.Log($"Sending effects ({string.Join(", ", effectIds)}) to {applyToPlayerId}");
        StatusEffectNetworkManager.Instance.ApplyToPlayerServerRpc(applyToPlayerId, string.Join(",", effectIds));

        OnApplyEffectTo(applyToPlayerId);
    }

}

public enum ApplyEffectOnActionType
{
    OnParry,
    OnAttack,
    OnHit,
    OnDeath,
    OnKill
}
