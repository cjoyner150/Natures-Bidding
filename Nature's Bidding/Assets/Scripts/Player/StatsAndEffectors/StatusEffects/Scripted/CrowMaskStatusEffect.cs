using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CrowMaskStatusEffect : StatusEffect
{
    public override StatsModifier GetStatsModifier() => null;

    IEnumerable<string> mods;

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null) return;

        NetworkObject playerNetworkObj = player.GetComponent<NetworkObject>();
        PlayerData playerData = PersistentPlayerRegistry.Instance.GetByClientId(playerNetworkObj.OwnerClientId);

        int randIdx = Random.Range(0, playerData.tarotCards.Count);

        mods = new string[] {
            playerData.tarotCards[randIdx]
        };

        StatusEffectManager.OnInitializeCompleted += RemoveModifiers;
    }

    private void RemoveModifiers()
    {
        StatusEffectManager.RemoveModifiers(mods);
        StatusEffectManager.OnInitializeCompleted -= RemoveModifiers;
    }
}
