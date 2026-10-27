using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GymnasiumServerHandler : BaseGameServerHandler<GymnasiumServerHandler>, IGameServerHandler
{

    private void Awake()
    {
        NetworkSessionManager.OnGymnasiumSessionHosted += OnSessionHosted;
    }

    private async void Start()
    {
        await NetworkSessionManager.Instance.WaitForAuth();
        await NetworkSessionManager.Instance.StartGymnasiumSession();

        PersistentGameStateManager.Instance.RegisterAuthData();
        PersistentGameStateManager.Instance.ClearLoadingState();
        PersistentGameStateManager.Instance.State = PersistentGameStateManager.GameState.Combat;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        NetworkSessionManager.OnGymnasiumSessionHosted -= OnSessionHosted;
    }

    protected override void OnNewPlayerConnected(ulong clientId, string authId, string playerName)
    {
        PersistentPlayerRegistry.Instance.RegisterPlayer(clientId, authId, playerName);
        SpawnAndRegisterPlayer(clientId);
    }

    public void SpawnPlayer()
    {

        var allPlayers = PersistentPlayerRegistry.Instance.GetAllPlayers();
        GameLogger.Log(LogSeverity.Debug, $"Registry has {allPlayers.Count} entries: " +
            string.Join(" | ", allPlayers.Select(p => $"clientId={p.clientId}, authId={p.authenticationId}, name={p.playerName}")));

        foreach (var data in allPlayers)
        {
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(data.clientId))
            {
                GameplaySpawnManager.Instance.SpawnPlayer(data.clientId);
            }
            else
            {
                GameLogger.Log(LogSeverity.Debug, $"Player {data.playerName} in registry but not connected — skipping spawn, they may rejoin.");
            }
        }
    }

    public void SpawnAndRegisterPlayer(ulong clientId)
    {
        if (!PersistentPlayerRegistry.Instance.HasPlayer(clientId))
        {
            GameLogger.Log(LogSeverity.Warning, $"Client {clientId} has no registry entry yet, cannot spawn.");
            return;
        }

        NetworkObject playerNetObj = GameplaySpawnManager.Instance.SpawnPlayer(clientId);

        var playerHandler = playerNetObj.GetComponent<PlayerNetworkBehavior>();
        //playerHandler.NotifyRegisteredRpc(clientId, _spawnedPlayers.Count);


    }

    void OnSessionHosted()
    {
        _ = UniTask.WaitUntil(() => PersistentGameStateManager.Instance != null).ContinueWith(() =>
        {
            GameLogger.Log(LogSeverity.Debug, "Gymnasium session hosted, spawning network singletons.");
            PersistentGameStateManager.Instance.SpawnNetworkSingletons();
        });
    }
    public void HandleInstantKill(PlayerHealth playerHealth)
    {
        throw new System.NotImplementedException();
    }

    public void OnPlayerDeath(ulong clientId)
    {
        throw new System.NotImplementedException();
    }
}
