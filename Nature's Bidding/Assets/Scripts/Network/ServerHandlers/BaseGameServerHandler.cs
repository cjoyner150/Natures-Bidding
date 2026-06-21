using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseGameServerHandler<T> : NetworkSingleton<T> where T : NetworkBehaviour
{
    [SerializeField] protected float acceptableAttackRange;

    protected virtual void RegisterCallbacks() { }
    protected virtual void UnregisterCallbacks() { }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer) RegisterCallbacks();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer) UnregisterCallbacks();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerId, float damage)
    {
        if (!IsServer) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(hitPlayerId, out var hitClient) ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(attackingPlayerId, out var attackingClient))
            return;

        var hitNetObj = hitClient.PlayerObject;
        var attackingNetObj = attackingClient.PlayerObject;

        if (hitNetObj == null || attackingNetObj == null) return;

        if (Vector3.Distance(hitNetObj.transform.position, attackingNetObj.transform.position) <= acceptableAttackRange)
        {
            OnPlayerHit(hitNetObj, attackingNetObj, damage);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHealServerRpc(ulong targetClientId, float amount)
    {
        if (!IsServer) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var targetClient)) return;

        var playerHealth = targetClient.PlayerObject.GetComponent<PlayerHealth>();
        playerHealth.health.Value = Mathf.Clamp(playerHealth.health.Value + amount, 0, playerHealth.maxHealth.Value);
    }

    protected virtual void OnPlayerHit(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage)
    {
        hitPlayer.GetComponent<PlayerHealth>()?.PlayerDamagedFeedbackClientRpc(attackingPlayer.transform.position);
    }

    private Dictionary<ulong, TaskCompletionSource<string>> playerNameRequests = new();

    public async Task<string> RequestPlayerNameByClientId(ulong clientId)
    {
        var tcs = new TaskCompletionSource<string>();
        playerNameRequests[clientId] = tcs;
        RequestPlayerNameRpc(clientId);
        return await tcs.Task;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayerNameRpc(ulong clientId, RpcParams rpcParams = default)
    {
        string playerName = null;
        var data = PersistentPlayerRegistry.Instance.GetByClientId(clientId);
        if (data != null)
            playerName = data.playerName;

        ulong senderId = rpcParams.Receive.SenderClientId;
        ReturnPlayerNameRpc(clientId, playerName,
            NetworkManager.Singleton.RpcTarget.Single(senderId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReturnPlayerNameRpc(ulong clientId, string playerName, RpcParams rpcParams = default)
    {
        if (playerNameRequests.TryGetValue(clientId, out var tcs))
        {
            tcs.SetResult(playerName);
            playerNameRequests.Remove(clientId);
        }
    }

    private TaskCompletionSource<List<PersistentPlayerData>> _playersRequestTcs;

    public async Task<List<PersistentPlayerData>> RequestPlayers()
    {
        _playersRequestTcs = new TaskCompletionSource<List<PersistentPlayerData>>();
        RequestPlayersRpc();
        return await _playersRequestTcs.Task;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayersRpc(RpcParams rpcParams = default)
    {
        var players = PersistentPlayerRegistry.Instance.GetAllPlayers();
        ulong[] clientIds = players.Select(p => p.clientId).ToArray();
        ulong senderId = rpcParams.Receive.SenderClientId;
        ReturnPlayersRpc(clientIds,
            NetworkManager.Singleton.RpcTarget.Single(senderId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReturnPlayersRpc(ulong[] clientIds, RpcParams rpcParams = default)
    {
        var players = clientIds
            .Select(id => PersistentPlayerRegistry.Instance.GetByClientId(id))
            .Where(p => p != null)
            .ToList();
        _playersRequestTcs?.SetResult(players);
    }

    [Rpc(SendTo.Server, DeferLocal = true, InvokePermission = RpcInvokePermission.Everyone)]
    public void SendAuthToServerRpc(string authId, string playerName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (PersistentPlayerRegistry.Instance.TryReconnectPlayer(clientId, authId, out var data))
        {
            Debug.Log($"Player {playerName} reconnected.");
            OnPlayerReconnected(clientId, data);
            return;
        }

        OnNewPlayerConnected(clientId, authId, playerName);
    }

    protected virtual void OnPlayerReconnected(ulong clientId, PersistentPlayerData data) { }

    protected virtual void OnNewPlayerConnected(ulong clientId, string authId, string playerName) { }
}

public interface IGameServerHandler
{
    void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerId, float damage);
    void RequestHealServerRpc(ulong targetClientId, float amount);
    void OnPlayerDeath(ulong clientId);
}