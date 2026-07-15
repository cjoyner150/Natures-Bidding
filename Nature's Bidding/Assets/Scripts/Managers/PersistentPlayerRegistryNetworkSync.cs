using Unity.Netcode;
using UnityEngine;

public class PlayerRegistryNetworkSync : NetworkSingleton<PlayerRegistryNetworkSync>
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);
    }

    // ---- Server-side entry points (called by PersistentPlayerRegistry) ----

    public void BroadcastPlayerData(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins)
    {
        if (!IsServer) return;
        BroadcastPlayerDataRpc(clientId, authId, playerName, playerIndex, gold, combatWins);
    }

    public void BroadcastGold(ulong clientId, int gold)
    {
        if (!IsServer) return;
        BroadcastGoldRpc(clientId, gold);
    }

    public void BroadcastCombatWins(ulong clientId, int wins)
    {
        if (!IsServer) return;
        BroadcastCombatWinsRpc(clientId, wins);
    }

    public void BroadcastItem(ulong clientId, string itemId, ItemType type)
    {
        if (!IsServer) return;
        BroadcastItemRpc(clientId, itemId, type);
    }

    public void BroadcastUnregister(ulong clientId, string authId)
    {
        if (!IsServer) return;
        BroadcastUnregisterRpc(clientId, authId);
    }

    public void BroadcastClear()
    {
        if (!IsServer) return;
        BroadcastClearRpc();
    }

    // Full snapshot to one late joiner
    public void SyncAllToClient(ulong targetClientId)
    {
        if (!IsServer) return;

        foreach (var data in PersistentPlayerRegistry.Instance.GetSnapshot())
        {
            var target = NetworkManager.Singleton.RpcTarget.Single(targetClientId, RpcTargetUse.Temp);

            SendPlayerDataToClientRpc(data.clientId, data.authenticationId, data.playerName,
                data.playerIndex, data.gold, data.combatWins, target);

            foreach (var mask in data.masks)
                SendItemToClientRpc(data.clientId, mask, ItemType.Mask, target);
            foreach (var card in data.tarotCards)
                SendItemToClientRpc(data.clientId, card, ItemType.TarotCard, target);
            foreach (var artifact in data.artifacts)
                SendItemToClientRpc(data.clientId, artifact, ItemType.Artifact, target);
        }
    }

    // ---- RPCs ----

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastPlayerDataRpc(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins)
        => PersistentPlayerRegistry.Instance.ApplyPlayerData(clientId, authId, playerName, playerIndex, gold, combatWins);

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendPlayerDataToClientRpc(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins, RpcParams rpcParams = default)
        => PersistentPlayerRegistry.Instance.ApplyPlayerData(clientId, authId, playerName, playerIndex, gold, combatWins);

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastGoldRpc(ulong clientId, int gold)
        => PersistentPlayerRegistry.Instance.ApplyGold(clientId, gold);

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastCombatWinsRpc(ulong clientId, int wins)
        => PersistentPlayerRegistry.Instance.ApplyCombatWins(clientId, wins);

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastItemRpc(ulong clientId, string itemId, ItemType type)
        => PersistentPlayerRegistry.Instance.ApplyItem(clientId, itemId, type);

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendItemToClientRpc(ulong clientId, string itemId, ItemType type, RpcParams rpcParams = default)
        => PersistentPlayerRegistry.Instance.ApplyItem(clientId, itemId, type);

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastUnregisterRpc(ulong clientId, string authId)
        => PersistentPlayerRegistry.Instance.ApplyUnregister(clientId, authId);

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastClearRpc()
        => PersistentPlayerRegistry.Instance.ApplyClear();
}