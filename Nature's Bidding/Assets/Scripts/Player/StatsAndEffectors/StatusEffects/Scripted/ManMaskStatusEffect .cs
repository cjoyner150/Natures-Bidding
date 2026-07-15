using UnityEngine;
using UnityEngine.InputSystem;

public class ManMaskStatusEffect : StatusEffect
{
    private PlayerHealth playerHealth;
    public override StatsModifier GetStatsModifier() => null;

    float explodeTimer;
    float explodeDamage;
    float explodeRadius;

    bool exploded = false;

    public ManMaskStatusEffect(float time, float damage, float radius)
    {
        explodeTimer = time;
        explodeDamage = damage;
        explodeRadius = radius;
    }

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null) return;

        playerHealth = player.GetComponent<PlayerHealth>();
    }

    public override void OnTick(float delta)
    {
        explodeTimer -= delta;

        if (explodeTimer <= 0 && !exploded)
        {
            exploded = true;
            playerHealth.Boom(explodeDamage, explodeRadius);
        }
    }

}
