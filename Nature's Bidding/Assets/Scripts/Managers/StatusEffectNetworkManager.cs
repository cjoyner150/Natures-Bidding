using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class StatusEffectNetworkManager : NetworkSingleton<StatusEffectNetworkManager>
{
    [SerializeField] ItemDatabase itemDatabase;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ApplyToPlayerServerRpc(ulong playerId, string effectIds)
    {
        Debug.Log($"Server received {(string.Join(", ", effectIds))} for {playerId}");
        if (NetworkManager.Singleton.ConnectedClientsIds.Contains(playerId))
        {
            ApplyToPlayerClientRpc(effectIds, RpcTarget.Single(playerId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    public void ApplyToPlayerClientRpc(string effectIds, RpcParams rpcParams)
    {
        Debug.Log($"I received {(string.Join(", ", effectIds))}");
        string[] ids = effectIds.Split(',');

        var playerStatusManager = NetworkManager.Singleton.ConnectedClients[NetworkManager.LocalClientId].PlayerObject.GetComponent<PlayerStatusEffectManager>();
        Debug.Log($"playerStatusManager is available: {playerStatusManager != null}, itemDatabase is available: {itemDatabase != null}");
        if (playerStatusManager != null && itemDatabase != null)
        {
            List<StatusEffectorSO> effectors = new();

            foreach(var id in ids)
            {
                effectors.Add(itemDatabase.Get(id));
            }

            playerStatusManager.AddModifiers(effectors);
        }
    }
}

