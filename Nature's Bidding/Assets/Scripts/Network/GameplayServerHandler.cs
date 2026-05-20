using Cinemachine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityUtils;

public class GameplayServerHandler : NetworkSingleton<GameplayServerHandler>
{
    List<PlayerServerInfo> players;
    List<PlayerServerInfo> alivePlayers;

    public static UnityEvent OnPlayerRegistered = new UnityEvent();
    public static UnityEvent OnAllPlayersRegistered = new UnityEvent();
    public static UnityEvent OnLocalPlayerDisconnect = new UnityEvent();

    [SerializeField] private CinemachineVirtualCamera winCamera;
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private float acceptableAttackRange;
    public int PlayersRequiredBeforeStart;

    [Range(1000, 20000)]
    [SerializeField] private int victoryLapDelay;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"GameplayServerHandler spawned. IsServer: {IsServer}");
        gameOverUI?.SetActive(false);

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            SpawnAllPlayers();
        }
        else
        {
            PersistentGameStateManager.Instance.OnGameplaySceneReady();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        players = new List<PlayerServerInfo>();
        alivePlayers = new List<PlayerServerInfo>();
    }

    #region Add Players

    private void SpawnAllPlayers()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            SpawnAndRegisterPlayer(client.ClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected. Current players spawned: {players.Count}");
        foreach (var p in players)
            Debug.Log($"  Already registered: {p.clientId}");
        SpawnAndRegisterPlayer(clientId);
    }    

    public void SpawnAndRegisterPlayer(ulong clientId)
    {
        if (!PlayerRegistry.Instance.Has(clientId))
        {
            Debug.LogWarning($"Client {clientId} has no registry entry yet, cannot spawn.");
            return;
        }

        NetworkObject playerNetObj = GameplaySpawnManager.Instance.SpawnPlayer(clientId);

        var info = PlayerRegistry.Instance.Get(clientId);
        if (players.Contains(info)) return;

        players.Add(info);
        int playersCount = players.Count;

        var playerHandler = playerNetObj.GetComponent<PlayerNetworkBehavior>();
        playerHandler.NotifyRegisteredRpc(info, playersCount);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void AllPlayersRegisteredClientRpc()
    {
        Debug.Log("All players registered!");
        OnAllPlayersRegistered?.Invoke();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void AllPlayersRegisteredServerRpc()
    {
        if (!IsServer) return;

        alivePlayers = players.Clone();
        AllPlayersRegisteredClientRpc();
    }

    #endregion

    #region Request Hit

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerIndex, float damage)
    {
        if (!IsServer) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(hitPlayerIndex, out var hitClient) ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(attackingPlayerId, out var attackingClient))
            return;

        var hitNetObj = hitClient.PlayerObject;
        var attackingNetObj = attackingClient.PlayerObject;

        if (hitNetObj == null || attackingNetObj == null) return;

        if (Vector3.Distance(hitNetObj.transform.position, attackingNetObj.transform.position) <= acceptableAttackRange)
        {
            var hitPlayerHealth = hitNetObj.GetComponent<PlayerHealth>();
            if (hitPlayerHealth == null) return;

            hitPlayerHealth.health.Value -= damage;
            hitPlayerHealth.PlayerDamagedFeedbackClientRpc(attackingNetObj.transform.position);
        }
    }

    #endregion

    #region Remove Player

    public void OnPlayerDeath(ulong clientId)
    {
        if (!IsServer) return;

        int index = alivePlayers.FindIndex(p => p.clientId == clientId);
        if (index != -1) alivePlayers.RemoveAt(index);

        if (alivePlayers.Count == 1)
        {
            OnRoundEndRpc(alivePlayers[0].clientId);
        }
    }

    public void OnClientDisconnected(ulong clientId)
    {
        PlayerRegistry.Instance.Remove(clientId);

        if (NetworkObject.OwnerClientId == clientId)
        {
            print("I am disconnecting!");
            return;
        }

        if (!IsServer) return;

        int index = alivePlayers.FindIndex(p => p.clientId == clientId);
        if (index != -1) alivePlayers.RemoveAt(index);
        
        index = players.FindIndex(p => p.clientId == clientId);
        if (index != -1) players.RemoveAt(index);

        if (alivePlayers.Count == 1)
        {
            ulong winningPlayerId = alivePlayers[0].clientId;

            NetworkManager.ConnectedClients[winningPlayerId].PlayerObject.GetComponent<PlayerHealth>().isRoundWinner.Value = true;

            OnRoundEndRpc(winningPlayerId);
        }
    }

    #endregion

    #region End Round

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void OnRoundEndRpc(ulong winningPlayer)
    {
        WinSequence(winningPlayer);
        NetworkManager.ConnectedClients[winningPlayer].PlayerObject.GetComponent<PlayerHealth>()?.OnWinRound(victoryLapDelay);
    }

    private async void WinSequence(ulong winningPlayer)
    {
        Transform winningPlayerTransform = NetworkManager.ConnectedClients[winningPlayer].PlayerObject.transform;

        winCamera.Follow = winningPlayerTransform;
        winCamera.LookAt = winningPlayerTransform;

        winCamera.enabled = true;

        await UniTask.Delay(victoryLapDelay);

        winCamera.enabled = false;
        gameOverUI?.SetActive(true);
    }

    #endregion

    #region Get Player Names

    private Dictionary<ulong, TaskCompletionSource<string>> playerNameRequests = new();

    /// <summary>
    /// Awaitable request for player name from the server
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
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
        PlayerServerInfo? player = null;

        if (players.Count > 0)
        {
            foreach (PlayerServerInfo p in players)
            {
                if (p.clientId == clientId)
                {
                    player = p;
                    break;
                }
            }
        }

        string playerName;

        if (player == null)
        {
            playerName = null;
        }
        else
        {
            playerName = player.Value.playerName.ToString();
        }

        ulong senderId = rpcParams.Receive.SenderClientId;
        ReturnPlayerNameRpc(clientId, playerName, NetworkManager.Singleton.RpcTarget.Single(senderId, RpcTargetUse.Temp));
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

    #endregion

    #region Get Player List

    private TaskCompletionSource<List<PlayerServerInfo>> playersRequestTcs;

    /// <summary>
    /// Awaitable request for the full list of players from the server
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public async Task<List<PlayerServerInfo>> RequestPlayers()
    {
        playersRequestTcs = new TaskCompletionSource<List<PlayerServerInfo>>();
        RequestPlayersRpc();
        return await playersRequestTcs.Task;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayersRpc(RpcParams rpcParams = default)
    {
        if (players.IsNullOrEmpty())
        {
            players = new List<PlayerServerInfo>();
        }

        PlayerServerInfo[] playersArray = new PlayerServerInfo[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            playersArray[i] = players[i];
        }
        ulong senderId = rpcParams.Receive.SenderClientId;
        ReturnPlayersRpc(playersArray, NetworkManager.Singleton.RpcTarget.Single(senderId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReturnPlayersRpc(PlayerServerInfo[] playersArray, RpcParams rpcParams = default)
    {
        playersRequestTcs?.SetResult(new List<PlayerServerInfo>(playersArray));
    }

    #endregion

}

public struct PlayerServerInfo : INetworkSerializable, IEquatable<PlayerServerInfo>
{
    public ulong clientId;
    public FixedString64Bytes playerAuthenticationId;
    public FixedString64Bytes playerName;

    public PlayerServerInfo(ulong _clientId, string _authId, string _name)
    {
        clientId = _clientId;
        playerAuthenticationId = _authId;
        playerName = _name;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerAuthenticationId);
        serializer.SerializeValue(ref playerName);
    }

    public bool Equals(PlayerServerInfo other)
    {
        return clientId == other.clientId;
    }
}
