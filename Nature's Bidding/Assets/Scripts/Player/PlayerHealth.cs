using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    public NetworkVariable<float> health =  new(100, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isInvulnerable = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isParrying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    NetworkObject selfNetworkObject;
    GameplayServerHandler gameplayServerHandler;
    
    public override void OnNetworkSpawn()
    {
        gameplayServerHandler = FindAnyObjectByType<GameplayServerHandler>();
        selfNetworkObject = GetComponent<NetworkObject>();
        
        if (IsOwner)
        {
            health.OnValueChanged += OnHealthChanged;
        }

        if (IsServer)
        {
            health.Value = 100;
        }

        
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner)
        {
            health.OnValueChanged -= OnHealthChanged;
        }
    }

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context)
    {
        if (!isInvulnerable.Value && !isParrying.Value)
        {
            context = IDamageable.HitCallbackContext.success;
            
            gameplayServerHandler.RequestHitPlayerServerRpc(fromPlayerId, selfNetworkObject.OwnerClientId, damage);
        }
        else
        {
            context = IDamageable.HitCallbackContext.failed;
        }
    }

    private void OnHealthChanged(float from, float to)
    {
        Debug.Log($"{selfNetworkObject.OwnerClientId}: My new health is {health.Value}");
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void PlayerDamagedFeedbackClientRpc()
    {
        if (!IsOwner) return;
        
        Debug.Log($"{selfNetworkObject.OwnerClientId}: I'm hit!");
        
    }
    
}
