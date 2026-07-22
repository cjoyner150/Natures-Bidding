using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class DebugOnActionStatusEffect : ApplyXOnActionStatusEffect
{

    public DebugOnActionStatusEffect(ApplyEffectOnActionType actionType, List<StatusEffectorSO> effectsToApply, bool applyToSelf = true)
        : base(actionType, effectsToApply, applyToSelf)
    {
    }

    protected override void OnApplyEffectTo(ulong targetId) {
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            Debug.Log($"[DebugOnActionStatusEffect] key={kvp.Key}, PlayerObject={(kvp.Value.PlayerObject != null ? kvp.Value.PlayerObject.name : "NULL")}");
        }

        var targetPlayer = NetworkManager.Singleton.ConnectedClients[targetId]?.PlayerObject;

        if (targetPlayer != null)
        {
            Debug.Log($"Applying debug effect on {actionType.ToString()} to target client with Id: {targetId} and Name: {targetPlayer.name}");
        }
        else Debug.LogError($"[DebugOnActionStatusEffect] No player object with clientId: {targetId}");
    }

}
