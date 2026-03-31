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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        selfPlayerHealth = GetComponent<PlayerHealth>();
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
                    
                    damageable.Hit(attackDamage, selfPlayerHealth.OwnerClientId, out IDamageable.HitCallbackContext ctx);
                    damagedObjectsOnThisAttack.Add(damageable);
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