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

    public static UnityEvent OnPlayerRegistered = new UnityEvent();
    public static UnityEvent OnAllPlayersRegistered = new UnityEvent();
    
    [SerializeField] private float acceptableAttackRange;
    [SerializeField] private int playersRequiredBeforeStart;


    protected override void Awake()
    {
        base.Awake();

        players = new List<PlayerServerInfo>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestHitPlayerServerRpc(ulong attackingPlayerId, ulong hitPlayerIndex, float damage)
    {
        if (!IsServer) return;
        
        var hitPlayer = NetworkManager.Singleton.ConnectedClients[hitPlayerIndex].PlayerObject.GetComponent<NetworkObject>();
        var attackingPlayer = NetworkManager.Singleton.ConnectedClients[attackingPlayerId].PlayerObject.GetComponent<NetworkObject>();

        if (Vector3.Distance(hitPlayer.transform.position, attackingPlayer.transform.position) <= acceptableAttackRange)
        {
            var hitPlayerHealth = hitPlayer.GetComponent<PlayerHealth>();
            
            hitPlayerHealth.health.Value -= damage;
            hitPlayerHealth.PlayerDamagedFeedbackClientRpc();
        }
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, DeferLocal = true)]
    public void RegisterPlayerOnServerRpc(PlayerServerInfo info, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (players.Contains(info)) return;

        players.Add(info);

        ulong senderId = rpcParams.Receive.SenderClientId;
        int playersCount = players.Count;

        NotifyPlayerRegisteredRpc(info, playersCount, NetworkManager.Singleton.RpcTarget.Single(senderId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void NotifyPlayerRegisteredRpc(PlayerServerInfo info, int playersCount, RpcParams rpcParams = default)
    {
        OnPlayerRegistered?.Invoke();
        Debug.Log($"Client with: clientId {info.clientId}, auth {info.playerAuthenticationId}, and name {info.playerName} has been registered on the server. There are now {playersCount} players.");

        if (playersCount >= playersRequiredBeforeStart) AllPlayersRegisteredRpc();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Everyone)]
    private void AllPlayersRegisteredRpc()
    {
        Debug.Log("All players registered!");
        OnAllPlayersRegistered?.Invoke();
    }

    // ----------------------------- Get Player Names ----------------------------- \\
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


    // ----------------------------- Get Player List ----------------------------- \\
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
