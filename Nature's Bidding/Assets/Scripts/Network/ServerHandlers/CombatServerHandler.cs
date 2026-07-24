using Cinemachine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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
        WaitUntilPlayersReady();
    }

    private async void WaitUntilPlayersReady()
    {
        await UniTask.WaitUntil(() => NetworkManager.ConnectedClientsList.All(c => c.PlayerObject != null));
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

        foreach (var data in PersistentPlayerRegistry.Instance.GetAllPlayers())
        {
            string maskIds = data.masks.Count > 0 ? string.Join(", ", data.masks) : "none";
            string tarotIds = data.tarotCards.Count > 0 ? string.Join(", ", data.tarotCards) : "none";
            string artifactIds = data.artifacts.Count > 0 ? string.Join(", ", data.artifacts) : "none";

            var maskEffectors = data.GetMaskEffectors();
            var tarotEffectors = data.GetTarotEffectors();
            var artifactEffectors = data.GetArtifactEffectors();

            string effectors = string.Join(", ",
                maskEffectors.ConvertAll(e => e != null ? e.Id : "null")
                    .Concat(tarotEffectors.ConvertAll(e => e != null ? e.Id : "null"))
                    .Concat(artifactEffectors.ConvertAll(e => e != null ? e.Id : "null")));

            if (string.IsNullOrWhiteSpace(effectors))
                effectors = "none";

            Debug.Log($"[CombatServerHandler] Player {data.clientId} ({data.playerName}) state after combat scene load | gold:{data.gold} wins:{data.combatWins} | masks:[{maskIds}] | tarot:[{tarotIds}] | artifacts:[{artifactIds}] | effectors:[{effectors}]");
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTickPlayerHealthServerRpc(ulong targetPlayerId, ulong fromPlayerId, float damage)
    {
        if (!IsServer) return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetPlayerId, out var hitClient)) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(fromPlayerId, out var fromClient)) return;

        var hitNetObj = hitClient.PlayerObject;
        var fromNetObj = fromClient.PlayerObject;

        if (hitNetObj == null || fromNetObj == null) return;

        OnTickDownPlayerHealth(hitNetObj, fromNetObj, damage);
    }

    protected void OnTickDownPlayerHealth(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage)
    {
        var targetHealth = hitPlayer.GetComponent<PlayerHealth>();
        if (targetHealth == null) return;

        float before = targetHealth.health.Value;
        targetHealth.health.Value -= damage;
        Debug.Log($"[CombatServerHandler] Health ticked. Before={before}, After={targetHealth.health.Value}, Damage={damage}");

        if (targetHealth.health.Value <= 0)
        {
            OnPlayerDeath(hitPlayer.OwnerClientId);
            NotifyPlayersOfDeath(targetHealth, attackingPlayer.GetComponent<PlayerHealth>());
        }
    }

    protected override void OnPlayerHit(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage, bool critical)
    {
        var targetHealth = hitPlayer.GetComponent<PlayerHealth>();
        if (targetHealth == null) return;

        targetHealth.health.Value -= damage;
        targetHealth.PlayerDamagedFeedbackClientRpc(attackingPlayer.transform.position, attackingPlayer.OwnerClientId, critical);

        if (targetHealth.health.Value <= 0)
        {
            Debug.Log($"[CombatServerHandler] Player died via direct hit. Victim={hitPlayer.OwnerClientId}, Killer={attackingPlayer.OwnerClientId}");
            OnPlayerDeath(hitPlayer.OwnerClientId);
            NotifyPlayersOfDeath(targetHealth, attackingPlayer.GetComponent<PlayerHealth>());
        }
    }

    public void NotifyPlayersOfDeath(PlayerHealth deadPlayer, PlayerHealth fromPlayer)
    {
        if (!IsServer) return;

        DeathSequence(deadPlayer, fromPlayer).Forget();
    }

    private async UniTaskVoid DeathSequence(PlayerHealth deadPlayer, PlayerHealth fromPlayer)
    {
        ulong victimId = deadPlayer.OwnerClientId;
        ulong killerId = fromPlayer.OwnerClientId;

        UniTask deathTask = deadPlayer.NotifyDeathAndAwaitAck(killerId);
        UniTask killTask = fromPlayer.NotifyKillCreditAndAwaitAck(victimId);

        bool timedOut = await UniTask.WhenAny(
            UniTask.WhenAll(deathTask, killTask),
            UniTask.Delay(TimeSpan.FromSeconds(5))
        ) == 1;

        if (timedOut)
            Debug.LogWarning($"[CombatServerHandler] Death sequence ack timeout for victim {victimId} — despawning anyway.");

        if (deadPlayer != null && deadPlayer.NetworkObject != null && deadPlayer.NetworkObject.IsSpawned)
            deadPlayer.NetworkObject.Despawn();
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
        if (IsServer && NetworkManager.ConnectedClients.TryGetValue(winningPlayer, out var winningClient) && winningClient.PlayerObject != null)
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

    protected override void OnPlayerReconnected(ulong clientId, PlayerData data)
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

    public void HandleInstantKill(PlayerHealth playerHealth)
    {
        if (!IsServer) return;

        if (playerHealth != null)
            playerHealth.health.Value = 0;
    }
}
    
