using UnityEngine;
using UnityEngine.InputSystem;

public class BatMaskStatusEffect : StatusEffect
{
    PlayerInputManager playerInput;
    public override StatsModifier GetStatsModifier() => null;

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null) return;

        playerInput = player.GetComponent<PlayerInputManager>();
        playerInput?.ReverseControls();
    }

    public override void OnEnd()
    {
        playerInput?.ResetControls();
    }
}
