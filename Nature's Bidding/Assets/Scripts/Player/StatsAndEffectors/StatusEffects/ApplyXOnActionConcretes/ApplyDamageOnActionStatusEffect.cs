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

    protected override void OnApplyEffectTo(long targetId) {

        if (targetId < 0) return; // To-do - implement bot effect application

        var targetPlayer = NetworkManager.Singleton.ConnectedClients[(ulong)targetId]?.PlayerObject;
        var fromPlayer = StatusEffectManager.gameObject.GetComponent<PlayerInputManager>()?.GetPlayerContext();

        if (targetPlayer != null && fromPlayer != null)
        {
            targetPlayer.GetComponent<PlayerHealth>().TickHealth(damage, fromPlayer);
        }
        else GameLogger.Log(LogSeverity.Error, $"Target or from player is null");
    }

}
