using Cinemachine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class CombatServerHandler : BaseGameServerHandler<CombatServerHandler>, IGameServerHandler
{
    List<ulong> alivePlayers = new();

    public static UnityEvent OnCombatBegin = new UnityEvent();


    [SerializeField] private CinemachineVirtualCamera winCamera;
    [SerializeField] private GameObject roundWinUI;
    [SerializeField] private TextMeshProUGUI roundWinTMP;
    [SerializeField] private GameObject[] hazardSystemGameObjects;
    private IHazardSystem[] hazardSystems;

    [Range(1000, 20000)]
    [SerializeField] private int victoryLapDelay;

    bool initializationComplete = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        roundWinUI?.SetActive(false);
        WaitUntilPlayersReady();
    }

    private async void WaitUntilPlayersReady()
    {
        await UniTask.WaitUntil(() => NetworkManager.ConnectedClientsList.All(c => c.PlayerObject != null));
        PersistentGameStateManager.Instance.OnCombatSceneReady();

        hazardSystems = hazardSystemGameObjects.Select(h => h.GetComponent<IHazardSystem>()).Where(h => h != null).ToArray();

        if (IsServer)
        {
            foreach (var hazard in hazardSystems)
            {
                hazard.StartHazard();
            }
        }

        initializationComplete = true;
    }

    private void OnSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
    List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        SpawnPlayers();
        LogPlayerData();

        CombatBeginClientRpc();
    }

    private static void LogPlayerData()
    {
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

            GameLogger.Log(LogSeverity.Debug, $"Player {data.clientId} ({data.playerName}) state after combat scene load | gold:{data.gold} wins:{data.combatWins} | masks:[{maskIds}] | tarot:[{tarotIds}] | artifacts:[{artifactIds}] | effectors:[{effectors}]");
        }
    }

    public void SpawnPlayers()
    {
        alivePlayers.Clear();

        var allPlayers = PersistentPlayerRegistry.Instance.GetAllPlayers();
        GameLogger.Log(LogSeverity.Debug, $"Registry has {allPlayers.Count} entries: " +
            string.Join(" | ", allPlayers.Select(p => $"clientId={p.clientId}, authId={p.authenticationId}, name={p.playerName}")));

        foreach (var data in allPlayers)
        {
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(data.clientId))
            {
                alivePlayers.Add(data.clientId);
                GameplaySpawnManager.Instance.SpawnPlayer(data.clientId);
            }
            else
            {
                GameLogger.Log(LogSeverity.Debug, $"Player {data.playerName} in registry but not connected — skipping spawn, they may rejoin.");
            }
        }
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

    void Update()
    {
        if (initializationComplete)
        {
            foreach (var hazard in hazardSystems)
            {
                hazard.TickHazard(Time.deltaTime);
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTickPlayerHealthServerRpc(long targetPlayerId, long fromPlayerId, float damage)
    {
        if (!IsServer) return;

        NetworkClient hitClient = null;
        NetworkClient fromClient = null;

        if (targetPlayerId >= 0)
        {
            NetworkManager.Singleton.ConnectedClients.TryGetValue((ulong)targetPlayerId, out hitClient);
        }

        if (fromPlayerId >= 0)
        {
            NetworkManager.Singleton.ConnectedClients.TryGetValue((ulong)fromPlayerId, out fromClient);
        }

        var hitNetObj = hitClient?.PlayerObject;
        var fromNetObj = fromClient?.PlayerObject;

        if (hitNetObj == null || fromNetObj == null) return;

        OnTickDownPlayerHealth(hitNetObj, fromNetObj, damage);
    }

    protected void OnTickDownPlayerHealth(NetworkObject hitPlayer, NetworkObject attackingPlayer, float damage)
    {
        var targetHealth = hitPlayer.GetComponent<PlayerHealth>();
        if (targetHealth == null) return;

        float before = targetHealth.health.Value;
        targetHealth.health.Value -= damage;
        GameLogger.Log(LogSeverity.Debug, $"Health ticked. Before={before}, After={targetHealth.health.Value}, Damage={damage}");

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
        targetHealth.PlayerDamagedFeedbackClientRpc(attackingPlayer.transform.position, (long)attackingPlayer.OwnerClientId, damage, critical);

        if (targetHealth.health.Value <= 0)
        {
            GameLogger.Log(LogSeverity.Debug, $"Player died via direct hit. Victim={hitPlayer.OwnerClientId}, Killer={attackingPlayer.OwnerClientId}");
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
        long victimId = (long)deadPlayer.OwnerClientId;
        long killerId = (long)fromPlayer.OwnerClientId;

        UniTask deathTask = deadPlayer.NotifyDeathAndAwaitAck(killerId);
        UniTask killTask = fromPlayer.NotifyKillCreditAndAwaitAck(victimId);

        bool timedOut = await UniTask.WhenAny(
            UniTask.WhenAll(deathTask, killTask),
            UniTask.Delay(TimeSpan.FromSeconds(5))
        ) == 1;

        if (timedOut)
            GameLogger.Log(LogSeverity.Warning, $"Death sequence ack timeout for victim {victimId} — despawning anyway.");

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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPlayerBoomServerRpc(long explodingPlayerId, float damage, float radius)
    {
        if (!IsServer) return;

        NetworkClient playerClient = null;
        if (explodingPlayerId >= 0)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue((ulong)explodingPlayerId, out playerClient)) return;
        }
        else
        {
            // Handle future implementation of bots here
            return;
        }

        var playerObject = playerClient.PlayerObject;
        var playerHealth = playerObject?.GetComponent<PlayerHealth>();

        if (playerHealth == null) return;

        Vector3 boomOrigin = playerObject.transform.position + (Vector3.up * .5f);

        NetworkVisualEffectManager.SpawnExplosionAtPosition.Invoke(boomOrigin);
        Collider[] hits = Physics.OverlapSphere(boomOrigin, radius, playersLayer);
        GameLogger.Log(LogSeverity.Debug, $"OverlapSphere found {hits.Length} colliders at {boomOrigin}, radius={radius}, layerMask={playersLayer.value}");

        HashSet<IDamageable> damagedObjectsOnThisAttack = new();

        foreach (Collider hit in hits)
        {
            GameObject go = hit.gameObject;
            var hitNetObj = go.GetComponentInParent<NetworkObject>();
            bool isSelf = hitNetObj != null && hitNetObj.OwnerClientId == (ulong)explodingPlayerId;

            if (isSelf) continue;

            GameLogger.Log(LogSeverity.Debug, $"Overlap collider: {go.name}, owner={hitNetObj?.OwnerClientId}, isSelf={isSelf}");
            UtilityExtensions.TryGetInParents<IDamageable>(go, out var damageable);

            if (damageable != null)
            {
                if (damagedObjectsOnThisAttack.Contains(damageable)) continue;
                damagedObjectsOnThisAttack.Add(damageable);

                if (damageable is PlayerHealth targetHealth)
                {
                    GameLogger.Log(LogSeverity.Debug, $"PlayerHealth found. Damaging...");
                    targetHealth.health.Value -= damage;
                    targetHealth.PlayerDamagedFeedbackClientRpc(boomOrigin, explodingPlayerId, damage, false);

                    if (targetHealth.health.Value <= 0)
                    {
                        OnPlayerDeath(targetHealth.OwnerClientId);
                        var explodingPlayerHealth = NetworkManager.Singleton.ConnectedClients[(ulong)explodingPlayerId].PlayerObject.GetComponent<PlayerHealth>();
                        NotifyPlayersOfDeath(targetHealth, explodingPlayerHealth);
                    }
                }
            }
        }

        HandleInstantKill(playerHealth);
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
        var playerHealth = NetworkManager.ConnectedClients[winningPlayer].PlayerObject.GetComponent<PlayerHealth>();
        playerHealth?.OnWinRound(victoryLapDelay);

        PlayerContext winningPlayerCtx = playerHealth?.gameObject.GetComponent<PlayerInputManager>()?.GetPlayerContext();

        await WinSequence(winningPlayerCtx, winningPlayer);

        if (this == null) return;

        if (IsServer)
        {
            foreach (var hazard in hazardSystems)
            {
                hazard.StopHazard();
            }
            PersistentGameStateManager.Instance.HandleCombatRoundEnded(winningPlayer).Forget();
        }
    }

    private async UniTask WinSequence(PlayerContext winningPlayerCtx, ulong winningPlayerId)
    {
        NetworkVisualEffectManager.SpawnConfettiEffectsOnPlayer?.Invoke(winningPlayerCtx);
        Transform winningPlayerTransform = NetworkManager.ConnectedClients[winningPlayerId]
            .PlayerObject.transform;

        winCamera.Follow = winningPlayerTransform;
        winCamera.LookAt = winningPlayerTransform;
        winCamera.enabled = true;

        roundWinUI.SetActive(true);

        PlayerData playerData = PersistentPlayerRegistry.Instance.GetByClientId(winningPlayerId);

        if (playerData == null)
        {
            GameLogger.Log(LogSeverity.Error, "No player data found for winning player.");
        }
        else
        {
            if (playerData.combatWins + 1 < 3)
            {
                roundWinTMP.text = $"{playerData.playerName} won the round! They have {playerData.combatWins + 1} / 3 wins.";
            }
            else
            {
                roundWinTMP.text = $"{playerData.playerName} has won the game!";
            }

        }
        await UniTask.Delay(victoryLapDelay);

    }

    protected override void OnPlayerReconnected(ulong clientId, PlayerData data)
    {
        GameLogger.Log(LogSeverity.Info, $"Player {data.playerName} rejoined mid-combat. Will respawn next scene.");
        PlayerRejoiningClientRpc(clientId);
    }

    protected override void OnNewPlayerConnected(ulong clientId, string authId, string playerName)
    {
        GameLogger.Log(LogSeverity.Warning, $"Unknown player {playerName} tried to join during combat. Ignoring.");
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayerRejoiningClientRpc(ulong clientId)
    {
        GameLogger.Log(LogSeverity.Info, $"Player {clientId} has rejoined and will respawn next scene.");

        // Hook into UI here to show "Player X has rejoined"
    }

    public void HandleInstantKill(PlayerHealth playerHealth)
    {
        if (!IsServer) return;
        if (playerHealth == null) return;

        playerHealth.health.Value = 0;

        OnPlayerDeath(playerHealth.OwnerClientId);
        EnvironmentalDeathSequence(playerHealth).Forget();
    }

    private async UniTaskVoid EnvironmentalDeathSequence(PlayerHealth deadPlayer)
    {
        await deadPlayer.NotifyDeathAndAwaitAck((long)deadPlayer.OwnerClientId);

        if (deadPlayer != null && deadPlayer.NetworkObject != null && deadPlayer.NetworkObject.IsSpawned)
            deadPlayer.NetworkObject.Despawn();
    }
}
    
