using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerAttackManager : NetworkBehaviour
{
    [SerializeField] private Transform attackTransform;
    [SerializeField] private PlayerHealth selfPlayerHealth;
    [SerializeField] private LayerMask attackableLayers;

    private bool isAttacking;

    private HashSet<IDamageable> damagedObjectsOnThisAttack = new HashSet<IDamageable>();

    [SerializeField] private float attackRadius;
    [SerializeField] private float attackLength;

    PlayerContext ctx;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        selfPlayerHealth = GetComponent<PlayerHealth>();
        if (IsOwner)
        {
            UpdateContextNextFrame();
        }
    }

    async void UpdateContextNextFrame()
    {
        await UniTask.NextFrame();
        ctx = selfPlayerHealth.GetPlayerContext();
    }

    public void BeginAttack()
    {
        damagedObjectsOnThisAttack.Clear();
        isAttacking = true;
    }

    public void EndAttack()
    {
        isAttacking = false;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        if (isAttacking)
        {
            RaycastHit[] hits = Physics.SphereCastAll(attackTransform.position, attackRadius, transform.forward, attackLength, attackableLayers);

            foreach (RaycastHit hit in hits)
            {
                GameObject go = hit.collider.gameObject;
                var damageable = go.GetComponent<IDamageable>();
                
                while (damageable == null && go.transform.parent != null)
                {
                    go = go.transform.parent.gameObject;
                    damageable = go.GetComponent<IDamageable>();
                }
                
                if (damageable != null)
                {
                    if (damagedObjectsOnThisAttack.Contains(damageable)) continue;
                    
                    HandleHitDamageableTarget(damageable, go);
                }
            }
        }
    }

    private void HandleHitDamageableTarget(IDamageable damageable, GameObject damagedObject)
    {
        bool crit = Random.value * 100f < ctx.playerStats.CritChance;
        float damage = ctx.playerStats.Damage;

        damage += ctx.playerStats.Momentum * (ctx.rb.linearVelocity.magnitude / 10f);
        damage += ctx.playerStats.ComboDamage * ctx.combo;
        damage *= crit ? ctx.playerStats.CritDamageMultiplier : 1;

        damageable.Hit(damage, selfPlayerHealth.OwnerClientId, out IDamageable.HitCallbackContext callbackContext);
        damagedObjectsOnThisAttack.Add(damageable);

        if (callbackContext == IDamageable.HitCallbackContext.success)
        {
            ctx.forceToAdd = Vector3.zero;
            ctx.rb.linearVelocity = (selfPlayerHealth.transform.position - damagedObject.transform.position).normalized * ctx.attackResponseForce;
            ctx.hitResponse = true;
            ctx.dashCDTimer = 0;

            ctx.comboCDTimer = ctx.comboCD;
            ctx.combo++;

            
            if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat)
            {
                if (ctx.playerStats.Stealing > 0) RequestStealServerRpc(OwnerClientId, damagedObject.GetComponent<NetworkObject>().OwnerClientId, (int)(ctx.playerStats.Stealing));
                if (ctx.playerStats.Lifesteal > 0) selfPlayerHealth.Heal(ctx.playerStats.Lifesteal);
            }
        }
        else if (callbackContext == IDamageable.HitCallbackContext.parried)
        {
            ctx.combo = 0;
            ctx.shouldStunSelf = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(attackTransform.position, attackRadius);
        Gizmos.DrawLine(attackTransform.position, attackTransform.position + (transform.forward * attackLength));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStealServerRpc(ulong thiefId, ulong targetId, int amount)
    {
        var target = PersistentPlayerRegistry.Instance.GetByClientId(targetId);
        if (target == null) return;

        int stolen = Mathf.Min(amount, target.gold);
        PersistentPlayerRegistry.Instance.TrySpendGold(targetId, stolen);
        PersistentPlayerRegistry.Instance.AddGold(thiefId, stolen);
    }
}