using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class TheMagicianTarotStatusEffect : StatusEffect
{
    PlayerContext playerContext;
    public override StatsModifier GetStatsModifier() => null;

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null)
        {
            GameLogger.Log(LogSeverity.Error, "No status effect manager found.");
            return;
        }

        playerContext = player.GetComponent<PlayerNetworkBehavior>()?.ctx;
        if (playerContext == null) { 
            GameLogger.Log(LogSeverity.Error, $"There is no player context found on {player.name}");
            return;
        }

        playerContext.teleportOnDash = true;
    }

    public override void OnEnd()
    {
        if (playerContext != null) playerContext.teleportOnDash = false;
    }
}
