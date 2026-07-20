using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityUtils;

public class LobbyServerHandler : BaseGameServerHandler<LobbyServerHandler>, IGameServerHandler
{
    private HashSet<ulong> _spawnedPlayers = new();
    private HashSet<ulong> _readiedPlayers = new();

    public static UnityEvent OnPlayerRegistered = new UnityEvent();
    public static UnityEvent OnEnoughPlayersRegistered = new UnityEvent();
    public static UnityEvent OnNoLongerEnoughPlayersRegistered = new UnityEvent();
    public static UnityEvent OnAllPlayersReadied = new UnityEvent();

    public int PlayersRequiredBeforeStart;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        PersistentGameStateManager.Instance.OnLobbySceneReady();
    }

    protected override void RegisterCallbacks()
    {
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    protected override void UnregisterCallbacks()
    {
        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    protected override void OnPlayerHit(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage, bool critical = false)
    {
        hitPlayer.GetComponent<PlayerHealth>()?.PlayerDamagedFeedbackClientRpc(attackingPlayer.transform.position, critical);
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        PersistentPlayerRegistry.Instance.UnregisterLobbyPlayer(clientId);
        _readiedPlayers.Remove(clientId);
        _spawnedPlayers.Remove(clientId);

        if (PersistentPlayerRegistry.Instance.GetAllPlayers().Count < PlayersRequiredBeforeStart)
            UnderPlayerRequirementClientRpc();
    }

    public void SpawnAndRegisterPlayer(ulong clientId)
    {
        if (!PersistentPlayerRegistry.Instance.HasPlayer(clientId))
        {
            Debug.LogWarning($"Client {clientId} has no registry entry yet, cannot spawn.");
            return;
        }

        if (_spawnedPlayers.Contains(clientId)) return;

        NetworkObject playerNetObj = GameplaySpawnManager.Instance.SpawnPlayer(clientId);
        _spawnedPlayers.Add(clientId);

        var playerHandler = playerNetObj.GetComponent<PlayerNetworkBehavior>();
        playerHandler.NotifyRegisteredRpc(clientId, _spawnedPlayers.Count);

        if (_spawnedPlayers.Count >= PlayersRequiredBeforeStart)
            EnoughPlayersRegisteredClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void EnoughPlayersRegisteredClientRpc()
    {
        OnEnoughPlayersRegistered?.Invoke();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void UnderPlayerRequirementClientRpc()
    {
        OnNoLongerEnoughPlayersRegistered?.Invoke();
    }

    [Rpc(SendTo.Server, DeferLocal = true, InvokePermission = RpcInvokePermission.Everyone)]
    public void PlayerReadiedServerRpc(RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        _readiedPlayers.Add(clientId);

        if (CheckPlayersReady())
            AllPlayersReadiedClientRpc();
    }

    private bool CheckPlayersReady() =>
        _readiedPlayers.Count >= PlayersRequiredBeforeStart &&
        PersistentPlayerRegistry.Instance.GetAllPlayers()
        .All(p => _readiedPlayers.Contains(p.clientId));

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void AllPlayersReadiedClientRpc()
    {
        OnAllPlayersReadied?.Invoke();
    }

    protected override void OnPlayerReconnected(ulong clientId, PersistentPlayerState data)
    {
        Debug.LogWarning($"Unexpected reconnect: {data.playerName} tried to reconnect in lobby. Ignoring.");
        PersistentPlayerRegistry.Instance.UnregisterLobbyPlayer(clientId);
        OnNewPlayerConnected(clientId, data.authenticationId, data.playerName);
    }

    protected override void OnNewPlayerConnected(ulong clientId, string authId, string playerName)
    {
        PersistentPlayerRegistry.Instance.RegisterPlayer(clientId, authId, playerName);
        SpawnAndRegisterPlayer(clientId);
        SyncLobbySnapshotWhenReady(clientId).Forget();
    }

    private async UniTaskVoid SyncLobbySnapshotWhenReady(ulong clientId)
    {
        await UniTask.WaitUntil(() => PlayerRegistryNetworkSync.Instance != null);

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        PlayerRegistryNetworkSync.Instance.SyncAllToClient(clientId);
    }

    public void OnPlayerDeath(ulong clientId) { }
}
