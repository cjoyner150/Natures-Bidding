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

    protected override void OnApplyEffectTo(ulong targetId) {
        var targetPlayer = NetworkManager.Singleton.ConnectedClients[targetId]?.PlayerObject;

        if (targetPlayer != null)
        {
            targetPlayer.GetComponent<PlayerHealth>().StunPlayer(stunTime);
        }
        else Debug.LogError($"[ApplyStunOnActionStatusEffect] No player object with clientId: {targetId}");
    }

}
