using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class TheStarTarotStatusEffect : StatusEffect
{
    private PlayerContext playerContext;
    public override StatsModifier GetStatsModifier() => null;

    float starRadius;
    float starDamage;
    float starSpeedThreshold;

    private float starDeactivationThreshold;

    private const float MinActiveDuration = 0.5f;
    private float _activeTimer = 0f;

    private bool effectEnabled = false;

    private const float DamageTickInterval = 0.1f;
    private float _damageTickTimer = 0f;
    private Dictionary<IDamageable, float> _accumulatedDamage = new();

    public TheStarTarotStatusEffect(float starRadius, float starDamage, float starSpeedThreshold)
    {
        this.starRadius = starRadius;
        this.starDamage = starDamage;
        this.starSpeedThreshold = starSpeedThreshold;

        starDeactivationThreshold = starSpeedThreshold * 0.65f;
    }

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null)
        {
            Debug.LogError($"[TheStarTarotStatusEffect] No status effect manager found.");
            return;
        }

        playerContext = player.GetComponent<PlayerNetworkBehavior>()?.ctx;
        if (playerContext == null)
        {
            Debug.LogError($"[TheStarTarotStatusEffect] There is no player context found on {player.name}");
            return;
        }
    }

    public override void OnTick(float delta)
    {
        float speed = playerContext.rb.linearVelocity.magnitude;

        UpdateEffectState(speed, delta);

        if (effectEnabled)
        {
            var hits = Physics.OverlapSphere(
                playerContext.modelHolder.position + (playerContext.modelHolder.up * 1.4f),
                starRadius,
                playerContext.playerLayerMask
            );

            foreach (var col in hits)
            {
                GameObject go = col.gameObject;
                UtilityExtensions.TryGetInParents<IDamageable>(go, out var damageable);
                if (damageable != null)
                {
                    _accumulatedDamage.TryGetValue(damageable, out float current);
                    _accumulatedDamage[damageable] = current + starDamage * delta;
                }
            }
        }

        _damageTickTimer += delta;
        if (_damageTickTimer >= DamageTickInterval)
        {
            _damageTickTimer = 0f;
            FlushDamage();
        }
    }

    private void UpdateEffectState(float speed, float delta)
    {
        if (!effectEnabled)
        {
            if (speed >= starSpeedThreshold)
            {
                effectEnabled = true;
                _activeTimer = 0f;
                NetworkVisualEffectManager.ToggleStarEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId, true);
            }
        }
        else
        {
            _activeTimer += delta;

            bool pastMinDuration = _activeTimer >= MinActiveDuration;
            bool belowDeactivationThreshold = speed < starDeactivationThreshold;

            if (pastMinDuration && belowDeactivationThreshold)
            {
                effectEnabled = false;
                NetworkVisualEffectManager.ToggleStarEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId, false);
            }
        }
    }

    private void FlushDamage()
    {
        if (_accumulatedDamage.Count == 0) return;

        foreach (var kvp in _accumulatedDamage)
        {
            var damageable = kvp.Key;
            float damage = kvp.Value;

            if (damage > 0f)
            {
                damageable.TickHealth(damage);
            }
        }

        _accumulatedDamage.Clear();
    }

    public override void OnEnd()
    {
        FlushDamage();

        if (effectEnabled)
        {
            effectEnabled = false;
            NetworkVisualEffectManager.ToggleStarEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId, false);
        }
    }
}