using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Xml.Linq;

public class CombatServerHandler : BaseGameServerHandler<CombatServerHandler>, IGameServerHandler
{
    List<ulong> alivePlayers = new();

    public static UnityEvent OnCombatBegin = new UnityEvent();

    [SerializeField] private CinemachineVirtualCamera winCamera;
    [SerializeField] private GameObject gameOverUI;

    [Range(1000, 20000)]
    [SerializeField] private int victoryLapDelay;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        gameOverUI?.SetActive(false);

        PersistentGameStateManager.Instance.OnCombatSceneReady();
    }

    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
    List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;

        alivePlayers.Clear();

        foreach (var data in PersistentPlayerRegistry.Instance.GetAllPlayers())
        {
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(data.clientId))
            {
                alivePlayers.Add(data.clientId);
                GameplaySpawnManager.Instance.SpawnPlayer(data.clientId);
            }
            else
            {
                Debug.Log($"Player {data.playerName} in registry but not connected — skipping spawn, they may rejoin.");
            }
        }

        CombatBeginClientRpc();
    }

    protected override void RegisterCallbacks()
    {
        NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    protected override void UnregisterCallbacks()
    {
        NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    protected override void OnPlayerHit(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage, bool critical)
    {
        var health = hitPlayer.GetComponent<PlayerHealth>();
        if (health == null) return;

        health.health.Value -= damage;
        health.PlayerDamagedFeedbackClientRpc(attackingPlayer.transform.position, critical);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void CombatBeginClientRpc()
    {
        OnCombatBegin?.Invoke();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        PersistentPlayerRegistry.Instance.MarkPlayerDisconnected(clientId);
        alivePlayers.Remove(clientId);

        if (alivePlayers.Count == 1)
        {
            ulong winningPlayerId = alivePlayers[0];
            NetworkManager.ConnectedClients[winningPlayerId].PlayerObject
                .GetComponent<PlayerHealth>().isRoundWinner.Value = true;
            OnRoundEndRpc(winningPlayerId);
        }
    }

    public void OnPlayerDeath(ulong clientId)
    {
        if (!IsServer) return;
        alivePlayers.Remove(clientId);

        if (alivePlayers.Count == 1)
            OnRoundEndRpc(alivePlayers[0]);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void OnRoundEndRpc(ulong winningPlayer)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(winningPlayer, out var winningClient) && winningClient.PlayerObject != null)
        {
            var winningHealth = winningClient.PlayerObject.GetComponent<PlayerHealth>();
            if (winningHealth != null)
                winningHealth.isRoundWinner.Value = true;
        }

        RoundEndSequence(winningPlayer);
    }

    async void RoundEndSequence(ulong winningPlayer)
    {
        NetworkManager.ConnectedClients[winningPlayer].PlayerObject.GetComponent<PlayerHealth>()?.OnWinRound(victoryLapDelay);
        await WinSequence(winningPlayer);

        if (this == null) return;

        if (IsServer)
            PersistentGameStateManager.Instance.HandleCombatRoundEnded(winningPlayer).Forget();
    }

    private async UniTask WinSequence(ulong winningPlayer)
    {
        NetworkVisualEffectManager.SpawnConfettiEffectsOnPlayer?.Invoke(winningPlayer);

        Transform winningPlayerTransform = NetworkManager.ConnectedClients[winningPlayer]
            .PlayerObject.transform;

        winCamera.Follow = winningPlayerTransform;
        winCamera.LookAt = winningPlayerTransform;
        winCamera.enabled = true;

        await UniTask.Delay(victoryLapDelay);

        if (this == null || gameOverUI == null) return;

        if (winCamera != null && winCamera.isActiveAndEnabled)
            winCamera.enabled = false;

        gameOverUI?.SetActive(true);
    }

    protected override void OnPlayerReconnected(ulong clientId, PersistentPlayerData data)
    {
        Debug.Log($"Player {data.playerName} rejoined mid-combat. Will respawn next scene.");
        PlayerRejoiningClientRpc(clientId);
    }

    protected override void OnNewPlayerConnected(ulong clientId, string authId, string playerName)
    {
        Debug.LogWarning($"Unknown player {playerName} tried to join during combat. Ignoring.");
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayerRejoiningClientRpc(ulong clientId)
    {
        Debug.Log($"Player {clientId} has rejoined and will respawn next scene.");

        // Hook into UI here to show "Player X has rejoined"
    }
}