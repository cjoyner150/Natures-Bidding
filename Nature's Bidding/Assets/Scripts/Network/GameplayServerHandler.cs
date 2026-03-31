using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameplayServerHandler : NetworkBehaviour
{
    [SerializeField] private float acceptableAttackRange;
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerIndex, float damage)
    {
        if (!IsServer) return;
        
        var hitPlayer = NetworkManager.Singleton.ConnectedClients[hitPlayerIndex].PlayerObject.GetComponent<NetworkObject>();
        var attackingPlayer = NetworkManager.Singleton.ConnectedClients[attackingPlayerId].PlayerObject.GetComponent<NetworkObject>();

        if (Vector3.Distance(transform.position, attackingPlayer.transform.position) <= acceptableAttackRange)
        {
            var hitPlayerHealth = hitPlayer.GetComponent<PlayerHealth>();
            
            hitPlayerHealth.health.Value -= damage;
            hitPlayerHealth.PlayerDamagedFeedbackClientRpc();
        }
    }
    
}
