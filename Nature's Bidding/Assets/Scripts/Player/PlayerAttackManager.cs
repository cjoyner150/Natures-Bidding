using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public class PlayerAttackManager : NetworkBehaviour
{
    [SerializeField] private Transform attackTransform;
    [SerializeField] private LayerMask attackableLayers;

    private bool isAttacking;

    private HashSet<IDamageable> damagedObjectsOnThisAttack = new HashSet<IDamageable>();
    
    [Header("Basic Attack Settings")]
    [SerializeField] private float attackRadius;
    [SerializeField] private float attackLength;

    [Header("Falling Slam Settings")]
    [SerializeField] private float fallingSlamRadius;
    [SerializeField] private float fallingSlamLength;

    private IDamageable selfDamageable;
    private PlayerContext ctx;

    //public override void OnNetworkSpawn()
    //{
    //    base.OnNetworkSpawn();
        
    //    selfDamageable = GetComponent<IDamageable>();

    //    if (IsOwner)
    //    {
    //        var inputManager = GetComponent<PlayerInputManager>();
    //        _ = UniTask.NextFrame().ContinueWith(() => ctx = inputManager.GetPlayerContext());
    //    }
    //}

    public void Initialize(PlayerContext ctx)
    {
        GameLogger.Log(LogSeverity.Debug, $"{gameObject.name} has initialized its attack manager.");
        selfDamageable = GetComponent<IDamageable>();

        this.ctx = ctx;
    }

    public void BeginAttack()
    {
        damagedObjectsOnThisAttack.Clear();
        isAttacking = true;

        NetworkVisualEffectManager.SpawnSlashEffectsOnPlayer?.Invoke(OwnerClientId, (int)(ctx.attackTime / ctx.attackSpeed * 1000));
    }

    public void EndAttack()
    {
        foreach (var damageable in damagedObjectsOnThisAttack)
        {
            if (damageable is PlayerHealth)
            {
                var health = damageable as PlayerHealth;
                PlayerCombatHooks.TriggerOnAttack(health.OwnerClientId);
            }
            else
            {
                PlayerCombatHooks.TriggerOnAttack(0);
            }
        }

        isAttacking = false;
    }

    public void FallingSlamAttack()
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position + (ctx.modelHolder.forward * fallingSlamRadius / 2f), 
            fallingSlamRadius, 
            ctx.modelHolder.forward, 
            fallingSlamLength, 
            attackableLayers
            );

        if (debugAttackCast)
            DrawSphereCastDebug(transform.position, fallingSlamRadius, ctx.modelHolder.forward, fallingSlamLength, hits);

        foreach (RaycastHit hit in hits)
        {
            GameObject go = hit.collider.gameObject;
            UtilityExtensions.TryGetInParents<IDamageable>(go, out var damageable);
            if (damageable != null)
            {
                HandleHitDamageableTarget(damageable, go);
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        if (isAttacking)
        {
            RaycastHit[] hits = Physics.SphereCastAll(attackTransform.position, attackRadius, ctx.modelHolder.forward, attackLength, attackableLayers);

            if (debugAttackCast)
                DrawSphereCastDebug(attackTransform.position, attackRadius, ctx.modelHolder.forward, attackLength, hits);

            foreach (RaycastHit hit in hits)
            {
                GameObject go = hit.collider.gameObject;
                UtilityExtensions.TryGetInParents<IDamageable>(go, out var damageable);

                if (damageable != null)
                {
                    if (damagedObjectsOnThisAttack.Contains(damageable)) continue;

                    HandleHitDamageableTarget(damageable, go);
                }
            }
        }
    }

    #region Debug Gizmo
    [Header("Debug")]
    [SerializeField] private bool debugAttackCast = false;

    private void DrawSphereCastDebug(Vector3 origin, float radius, Vector3 direction, float distance, RaycastHit[] hits)
    {
        bool didHit = hits.Length > 0;
        Color color = didHit ? Color.red : Color.green;
        float duration = 1f;

        Vector3 endPos = origin + direction.normalized * distance;

        // Start and end spheres
        DrawWireSphere(origin, radius, color, duration);
        DrawWireSphere(endPos, radius, color, duration);

        // Connecting lines along the cast path (top, bottom, and side rails to show the capsule shape)
        Vector3 up = Vector3.up * radius;
        Vector3 right = Vector3.Cross(direction.normalized, Vector3.up).normalized * radius;

        Debug.DrawLine(origin + up, endPos + up, color, duration);
        Debug.DrawLine(origin - up, endPos - up, color, duration);
        Debug.DrawLine(origin + right, endPos + right, color, duration);
        Debug.DrawLine(origin - right, endPos - right, color, duration);

        // Mark each actual hit point distinctly
        foreach (var hit in hits)
        {
            Debug.DrawLine(hit.point, hit.point + Vector3.up * 0.3f, Color.yellow, duration);
            Debug.DrawLine(hit.point + Vector3.left * 0.15f, hit.point + Vector3.right * 0.15f, Color.yellow, duration);
        }
    }

    private void DrawWireSphere(Vector3 center, float radius, Color color, float duration, int segments = 16)
    {
        DrawCircle(center, radius, Vector3.up, color, duration, segments);
        DrawCircle(center, radius, Vector3.right, color, duration, segments);
        DrawCircle(center, radius, Vector3.forward, color, duration, segments);
    }

    private void DrawCircle(Vector3 center, float radius, Vector3 normal, Color color, float duration, int segments)
    {
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
    #endregion

    private void HandleHitDamageableTarget(IDamageable damageable, GameObject damagedObject)
    {
        bool crit = Random.value * 100f < ctx.playerStats.CritChance;
        float damage = ctx.playerStats.Damage;

        damage += ctx.playerStats.Momentum * (ctx.rb.linearVelocity.magnitude / 10f);
        damage += ctx.playerStats.ComboDamage * ctx.combo;
        damage *= crit ? ctx.playerStats.CritDamageMultiplier : 1;

        ulong selfClientId = 0;
        if (selfDamageable is PlayerHealth)
        {
            selfClientId = ((PlayerHealth)selfDamageable).OwnerClientId;
        }

        damageable.Hit(damage, selfClientId, out IDamageable.HitCallbackContext callbackContext, crit);
        damagedObjectsOnThisAttack.Add(damageable);

        if (callbackContext == IDamageable.HitCallbackContext.success)
        {
            ctx.forceToAdd = Vector3.zero;
            ctx.rb.linearVelocity = (ctx.modelHolder.position - damagedObject.transform.position).normalized * ctx.attackResponseForce;
            ctx.hitResponse = true;
            ctx.dashCDTimer = 0;

            ctx.comboCDTimer = ctx.comboCD;
            ctx.combo++;

            
            if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat)
            {
                if (ctx.playerStats.Stealing > 0) 
                {
                    var damagedEffectable = damagedObject.GetComponent<IEffectable>();

                    int retries = 10;
                    while (retries >= 0 && damagedEffectable == null && damagedObject.transform.parent != null)
                    {
                        damagedObject = damagedObject.transform.parent.gameObject;
                        damagedEffectable = damagedObject.GetComponent<IEffectable>();
                        retries--;
                    }

                    var damagedNetworkObject = damagedObject.GetComponent<NetworkObject>();

                    ulong stealTarget = damagedNetworkObject != null ? damagedNetworkObject.OwnerClientId : 0;

                    if (damagedEffectable != null) damagedEffectable.StealFrom(OwnerClientId, stealTarget, (int)(ctx.playerStats.Stealing));
                }
                if (ctx.playerStats.Lifesteal > 0) selfDamageable.Heal(ctx.playerStats.Lifesteal);
            }
        }
        else if (callbackContext == IDamageable.HitCallbackContext.parried)
        {
            selfDamageable.Stun(0);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(attackTransform.position, attackRadius);
        Gizmos.DrawLine(attackTransform.position, attackTransform.position + (transform.forward * attackLength));
    }

}