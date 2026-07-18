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

    private bool effectEnabled = false;

    private const float DamageTickInterval = 0.1f; // 10 damage ticks per second
    private float _damageTickTimer = 0f;
    private Dictionary<IDamageable, float> _accumulatedDamage = new();

    public TheStarTarotStatusEffect(float starRadius, float starDamage, float starSpeedThreshold)
    {
        this.starRadius = starRadius;
        this.starDamage = starDamage;
        this.starSpeedThreshold = starSpeedThreshold;
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
        bool isFast = playerContext.rb.linearVelocity.magnitude >= starSpeedThreshold;

        if (isFast)
        {
            var hits = Physics.OverlapSphere(
                playerContext.modelHolder.position + (playerContext.modelHolder.up *  1.4f),
                starRadius,
                playerContext.playerLayerMask
            );

            // Debug draw the overlap sphere in the Game view (enable Gizmos in the Game window).
            //DebugDrawSphere(playerContext.modelHolder.position + (playerContext.modelHolder.up * 1.4f), starRadius, Color.cyan, 0.1f);

            foreach (var col in hits)
            {
                Debug.Log($"[TheStarTarotStatusEffect] Detected hit on {col.gameObject.name}");
                GameObject go = col.gameObject;
                UtilityExtensions.TryGetInParents<IDamageable>(go, out var damageable);
                if (damageable != null)
                {
                    Debug.Log($"[TheStarTarotStatusEffect] Found damageable on {col.gameObject.name}");
                    _accumulatedDamage.TryGetValue(damageable, out float current);
                    _accumulatedDamage[damageable] = current + starDamage * delta;
                }
            }

            if (!effectEnabled)
            {
                effectEnabled = true;
                NetworkVisualEffectManager.ToggleStarEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId, true);
            }
        }
        else if (effectEnabled)
        {
            effectEnabled = false;
            NetworkVisualEffectManager.ToggleStarEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId, false);
        }

        _damageTickTimer += delta;
        if (_damageTickTimer >= DamageTickInterval)
        {
            _damageTickTimer = 0f;
            FlushDamage();
        }
    }

    private void FlushDamage()
    {
        if (_accumulatedDamage.Count == 0) return;

        Debug.Log($"[TheStarTarotStatusEffect] Flushing damage for {_accumulatedDamage.Count} target(s).");

        foreach (var kvp in _accumulatedDamage)
        {
            var damageable = kvp.Key;
            float damage = kvp.Value;

            Debug.Log($"[TheStarTarotStatusEffect] Target={damageable}, AccumulatedDamage={damage}");

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

    // Draw an approximate wireframe sphere using Debug.DrawLine so the OverlapSphere is visible in the Game view.
    // Note: enable Gizmos in the Game window to see these lines while playing in the Editor.
    private void DebugDrawSphere(Vector3 center, float radius, Color color, float duration = 0.05f, int segments = 24)
    {
        // draw circles in XY, XZ, YZ planes
        DrawCircle(center, radius, Vector3.up, color, duration, segments);
        DrawCircle(center, radius, Vector3.right, color, duration, segments);
        DrawCircle(center, radius, Vector3.forward, color, duration, segments);
    }

    private void DrawCircle(Vector3 center, float radius, Vector3 normal, Color color, float duration, int segments)
    {
        // find two perpendicular axes for the plane
        Vector3 axisA = Vector3.Cross(normal, Vector3.up);
        if (axisA.sqrMagnitude < 0.001f) axisA = Vector3.Cross(normal, Vector3.right);
        axisA.Normalize();
        Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

        Vector3 prevPoint = center + (axisA * radius);
        float angleStep = 360f / segments;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            Vector3 nextPoint = center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius;
            Debug.DrawLine(prevPoint, nextPoint, color, duration);
            prevPoint = nextPoint;
        }
    }
}