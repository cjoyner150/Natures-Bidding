using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkVisualEffectManager : NetworkSingleton<NetworkVisualEffectManager>
{

    // Locally call event from anywhere in normal code with the clientId
    public static Action<PlayerContext> SpawnDashEffectsOnPlayer;
    public static Action<PlayerContext> SpawnTeleportEffectsOnPlayer;
    public static Action<PlayerContext> SpawnJumpEffectsOnPlayer;
    public static Action<PlayerContext> SpawnParrySuccessReactEffectsOnPlayer;
    public static Action<PlayerContext> SpawnConfettiEffectsOnPlayer;
    public static Action<PlayerContext> SpawnBatConfusionEffectsOnPlayer;
    public static Action<PlayerContext> RemoveBatConfusionEffectsOnPlayer;

    public static Action<PlayerContext, int> SpawnSlashEffectsOnPlayer;
    public static Action<PlayerContext, int> SpawnParryEffectsOnPlayer;
    public static Action<PlayerContext, int> SpawnStunEffectsOnPlayer;

    public static Action<PlayerContext, bool> ToggleStarEffectsOnPlayer;
    public static Action<PlayerContext, bool, Vector3, float> SpawnHitReactionEffectsOnPlayer;
    public static Action<Vector3> SpawnExplosionAtPosition;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);

        SpawnSlashEffectsOnPlayer += OnSpawnSlashEffectOnPlayer;
        SpawnParryEffectsOnPlayer += OnSpawnParryEffectOnPlayer;
        SpawnParrySuccessReactEffectsOnPlayer += OnSpawnParrySuccessReactEffectsOnPlayer;
        SpawnDashEffectsOnPlayer += OnSpawnDashEffectsOnPlayer;
        SpawnTeleportEffectsOnPlayer += OnSpawnTeleportEffectsOnPlayer;
        ToggleStarEffectsOnPlayer += OnToggleStarEffectsOnPlayer;
        SpawnJumpEffectsOnPlayer += OnSpawnJumpEffectsOnPlayer;
        SpawnStunEffectsOnPlayer += OnSpawnStunEffectsOnPlayer;
        SpawnConfettiEffectsOnPlayer += OnSpawnConfettiEffectsOnPlayer;
        SpawnBatConfusionEffectsOnPlayer += OnSpawnBatConfusionEffectsOnPlayer;
        RemoveBatConfusionEffectsOnPlayer += OnRemoveBatConfusionEffectsOnPlayer;
        SpawnHitReactionEffectsOnPlayer += OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition += OnSpawnExplosionAtPosition;
    }

    public override void OnNetworkDespawn()
    {
        SpawnSlashEffectsOnPlayer -= OnSpawnSlashEffectOnPlayer;
        SpawnParryEffectsOnPlayer -= OnSpawnParryEffectOnPlayer;
        SpawnParrySuccessReactEffectsOnPlayer -= OnSpawnParrySuccessReactEffectsOnPlayer;
        SpawnDashEffectsOnPlayer -= OnSpawnDashEffectsOnPlayer;
        SpawnTeleportEffectsOnPlayer -= OnSpawnTeleportEffectsOnPlayer;
        ToggleStarEffectsOnPlayer -= OnToggleStarEffectsOnPlayer;
        SpawnJumpEffectsOnPlayer -= OnSpawnJumpEffectsOnPlayer;
        SpawnStunEffectsOnPlayer -= OnSpawnStunEffectsOnPlayer;
        SpawnConfettiEffectsOnPlayer -= OnSpawnConfettiEffectsOnPlayer;
        SpawnBatConfusionEffectsOnPlayer -= OnSpawnBatConfusionEffectsOnPlayer;
        RemoveBatConfusionEffectsOnPlayer -= OnRemoveBatConfusionEffectsOnPlayer;
        SpawnHitReactionEffectsOnPlayer -= OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition -= OnSpawnExplosionAtPosition;
    }

    public void OnSpawnExplosionAtPosition(Vector3 spawnPos) 
    {
        GameLogger.Log(LogSeverity.Debug, "Server has instructed an explosion to be spawned."); 
        var localVFXManager = GetFirstValidEffectManager();
        if (localVFXManager != null) localVFXManager.SpawnExplosionParticles(spawnPos);

        SpawnExplosionAtPositionClientRpc(spawnPos);
    }
    public void OnSpawnHitReactionEffectsOnPlayer(PlayerContext clientCtx, bool critical, Vector3 fromPos, float damage)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnHitReactParticles(critical, fromPos, damage);
        
        
        if (TryGetClientId(clientCtx, out var clientId))
        {
            SpawnHitReactionEffectsClientRpc(clientId, critical, fromPos, damage);
            GameLogger.Log(LogSeverity.Debug, $"OnSpawnHitReaction received for client {clientId}");
        }
    }

    public void OnSpawnSlashEffectOnPlayer(PlayerContext clientCtx, int milliseconds)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnSlashEffectParticles(milliseconds);

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnSlashEffectClientRpc(clientId, milliseconds);
    }

    public void OnSpawnParryEffectOnPlayer(PlayerContext clientCtx, int milliseconds)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnParryEffectParticles(milliseconds);

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnParryEffectClientRpc(clientId, milliseconds);
    }

    public void OnSpawnParrySuccessReactEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnParrySuccessReactionParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnParrySuccessReactEffectsClientRpc(clientId);
    }

    public void OnSpawnDashEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnDashParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnDashEffectsClientRpc(clientId);
    }

    public void OnSpawnTeleportEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnTeleportParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnTeleportEffectsClientRpc(clientId);
    }

    public void OnToggleStarEffectsOnPlayer(PlayerContext clientCtx, bool enabled)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.ToggleStarParticles(enabled);

        if (TryGetClientId(clientCtx, out var clientId))
            ToggleStarEffectsClientRpc(clientId, enabled);
    }

    public void OnSpawnJumpEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnJumpParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnJumpEffectsClientRpc(clientId);
    }

    public void OnSpawnStunEffectsOnPlayer(PlayerContext clientCtx, int milliseconds)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnStunParticles(milliseconds);

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnStunEffectsClientRpc(clientId, milliseconds);
    }

    public void OnSpawnConfettiEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnConfettiEffectsClientRpc(clientId);
    }

    public void OnSpawnBatConfusionEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.SpawnBatConfusionParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            SpawnBatConfusionEffectsClientRpc(clientId);
    }

    public void OnRemoveBatConfusionEffectsOnPlayer(PlayerContext clientCtx)
    {
        var localVFXManager = GetLocalEffectManagerByCtx(clientCtx);
        if (localVFXManager != null) localVFXManager.RemoveBatConfusionParticles();

        if (TryGetClientId(clientCtx, out var clientId))
            RemoveBatConfusionEffectsClientRpc(clientId);
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnExplosionAtPositionClientRpc(Vector3 spawnPos)
    {
        var playerEffectManager = GetFirstValidEffectManager();

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnExplosionParticles(spawnPos);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnHitReactionEffectsClientRpc(ulong clientId, bool critical, Vector3 fromPos, float damage)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnHitReactParticles(critical, fromPos, damage);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnSlashEffectClientRpc(ulong clientId, int milliseconds)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnSlashEffectParticles(milliseconds);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnParryEffectClientRpc(ulong clientId, int milliseconds)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnParryEffectParticles(milliseconds);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnParrySuccessReactEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnParrySuccessReactionParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnDashEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnDashParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnTeleportEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnTeleportParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void ToggleStarEffectsClientRpc(ulong clientId, bool enabled)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.ToggleStarParticles(enabled);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnJumpEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnJumpParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnStunEffectsClientRpc(ulong clientId, int milliseconds)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnStunParticles(milliseconds);
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnConfettiEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            //playerEffectManager.SpawnConfettiParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnBatConfusionEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnBatConfusionParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveBatConfusionEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.RemoveBatConfusionParticles();
        }
        else
        {
            GameLogger.Log(LogSeverity.Error, "Player Visual Effect Manager not found.");
        }
    }


    private bool TryGetClientId(PlayerContext clientCtx, out ulong clientId)
    {
        if (clientCtx.playerDamageable is NetworkBehaviour)
        {
            clientId = (clientCtx.playerDamageable as NetworkBehaviour).OwnerClientId;
            return true;
        }
        else
        {
            clientId = 0;
            return false;
        }
    }

    private PlayerVisualEffectManager GetPlayerEffectManagerById(ulong id) => NetworkManager.Singleton.ConnectedClients[id]?.PlayerObject?.GetComponent<PlayerVisualEffectManager>();
    private PlayerVisualEffectManager GetLocalEffectManagerByCtx(PlayerContext ctx) => ctx.playerAttackManager?.gameObject.GetComponent<PlayerVisualEffectManager>();
    private PlayerVisualEffectManager GetFirstValidEffectManager() => NetworkManager.Singleton.ConnectedClients.Values.First(p => p.PlayerObject != null).PlayerObject.GetComponent<PlayerVisualEffectManager>();
    
}

