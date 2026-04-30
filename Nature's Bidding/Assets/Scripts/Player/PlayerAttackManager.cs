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
    [SerializeField] private float attackDamage;

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
                    
                    damageable.Hit(attackDamage, selfPlayerHealth.OwnerClientId, out IDamageable.HitCallbackContext callbackContext);
                    damagedObjectsOnThisAttack.Add(damageable);

                    if (callbackContext == IDamageable.HitCallbackContext.success)
                    {
                        ctx.forceToAdd = Vector3.zero;
                        ctx.rb.linearVelocity = (selfPlayerHealth.transform.position - go.transform.position).normalized * ctx.attackResponseForce;
                        ctx.hitResponse = true;
                        ctx.dashCDTimer = 0;
                    }
                }
            }
        }
        
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(attackTransform.position, attackRadius);
        Gizmos.DrawLine(attackTransform.position, attackTransform.position + (transform.forward * attackLength));
    }

}