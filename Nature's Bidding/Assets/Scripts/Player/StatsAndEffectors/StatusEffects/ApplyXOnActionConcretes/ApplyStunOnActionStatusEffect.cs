using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class ApplyStunOnActionStatusEffect : ApplyXOnActionStatusEffect
{
    protected float stunTime;

    public ApplyStunOnActionStatusEffect(float additionalStunTime, ApplyEffectOnActionType actionType, List<StatusEffectorSO> effectsToApply, bool applyToSelf = true)
        : base(actionType, effectsToApply, applyToSelf)
    {
        stunTime = additionalStunTime;
    }

    protected override void OnApplyEffectTo(long targetId) {
        if (targetId < 0) return; // Todo - Implement effects for bots

        var targetPlayer = NetworkManager.Singleton.ConnectedClients[(ulong)targetId]?.PlayerObject;

        if (targetPlayer != null)
        {
            targetPlayer.GetComponent<IEffectable>().Stun(stunTime);
        }
        else GameLogger.Log(LogSeverity.Error, $"No player object with clientId: {targetId}");
    }

}
