using UnityEngine;
using UnityEngine.InputSystem;

public class BatMaskStatusEffect : StatusEffect
{
    PlayerInputManager _playerInput;
    private bool _initialized = false;
    public override StatsModifier GetStatsModifier() => null;

    public override void OnTick(float delta)
    {
        if (_initialized) return;

        var player = GetAttachedPlayer();
        if (player == null) return;

        _playerInput = player.GetComponent<PlayerInputManager>();
        _playerInput?.ReverseControls();
        _initialized = true;
    }

    public override void OnEnd()
    {
        _playerInput?.ResetControls();
    }
}
