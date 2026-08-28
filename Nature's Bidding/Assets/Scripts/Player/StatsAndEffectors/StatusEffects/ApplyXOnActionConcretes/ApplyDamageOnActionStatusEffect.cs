using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class ApplyDamageOnActionStatusEffect : ApplyXOnActionStatusEffect
{
    protected float damage;

    public ApplyDamageOnActionStatusEffect(float damage, ApplyEffectOnActionType actionType, List<StatusEffectorSO> effectsToApply, bool applyToSelf = true)
        : base(actionType, effectsToApply, applyToSelf)
    {
        this.damage = damage;
    }

    protected override void OnApplyEffectTo(ulong targetId) {
        var targetPlayer = NetworkManager.Singleton.ConnectedClients[targetId]?.PlayerObject;

        if (targetPlayer != null)
        {
            targetPlayer.GetComponent<PlayerHealth>().TickHealth(damage, NetworkManager.Singleton.LocalClientId);
        }
        else GameLogger.Log(LogSeverity.Error, $"No player object with clientId: {targetId}");
    }

}
