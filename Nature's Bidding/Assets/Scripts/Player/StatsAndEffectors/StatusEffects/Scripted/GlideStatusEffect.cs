using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GlideStatusEffect : StatusEffect
{
    PlayerContext playerContext;
    float originalDrag;
    public override StatsModifier GetStatsModifier() => null;

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null)
        {
            Debug.LogError($"[GlideStatusEffect] No status effect manager found.");
            return;
        }

        playerContext = player.GetComponent<PlayerNetworkBehavior>()?.ctx;
        if (playerContext == null) { 
            Debug.LogError($"[GlideStatusEffect] There is no player context found on {player.name}");
            return;
        }

        originalDrag = playerContext.groundDrag;
        playerContext.groundDrag = 0;
    }

    public override void OnEnd()
    {
        if (playerContext != null) playerContext.groundDrag = originalDrag;
    }
}
