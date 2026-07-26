using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkVisualEffectManager : NetworkSingleton<NetworkVisualEffectManager>
{

    // Locally call event from anywhere in normal code with the clientId
    public static Action<ulong> SpawnDashEffectsOnPlayer;
    public static Action<ulong> SpawnTeleportEffectsOnPlayer;
    public static Action<ulong> SpawnJumpEffectsOnPlayer;
    public static Action<ulong> SpawnParrySuccessReactEffectsOnPlayer;
    public static Action<ulong> SpawnConfettiEffectsOnPlayer;
    public static Action<ulong> SpawnBatConfusionEffectsOnPlayer;
    public static Action<ulong> RemoveBatConfusionEffectsOnPlayer;

    public static Action<ulong, int> SpawnSlashEffectsOnPlayer;
    public static Action<ulong, int> SpawnParryEffectsOnPlayer;
    public static Action<ulong, int> SpawnStunEffectsOnPlayer;

    public static Action<ulong, bool> ToggleStarEffectsOnPlayer;
    public static Action<ulong, bool, Vector3, float> SpawnHitReactionEffectsOnPlayer;
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
        var localVFXManager = GetFirstValidEffectManager();
        if (localVFXManager != null) localVFXManager.SpawnExplosionParticles(spawnPos);

        SpawnExplosionAtPositionClientRpc(spawnPos);
    }
    public void OnSpawnHitReactionEffectsOnPlayer(ulong clientId, bool critical, Vector3 fromPos, float damage)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnHitReactParticles(critical, fromPos, damage);

        SpawnHitReactionEffectsClientRpc(clientId, critical, fromPos, damage);
    }

    public void OnSpawnSlashEffectOnPlayer(ulong clientId, int milliseconds)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnSlashEffectParticles(milliseconds);

        SpawnSlashEffectClientRpc(clientId, milliseconds);
    }

    public void OnSpawnParryEffectOnPlayer(ulong clientId, int milliseconds)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnParryEffectParticles(milliseconds);

        SpawnParryEffectClientRpc(clientId, milliseconds);
    }

    public void OnSpawnParrySuccessReactEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnParrySuccessReactionParticles();

        SpawnParrySuccessReactEffectsClientRpc(clientId);
    }

    public void OnSpawnDashEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnDashParticles();

        SpawnDashEffectsClientRpc(clientId);
    }

    public void OnSpawnTeleportEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnTeleportParticles();

        SpawnTeleportEffectsClientRpc(clientId);
    }

    public void OnToggleStarEffectsOnPlayer(ulong clientId, bool enabled)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.ToggleStarParticles(enabled);

        ToggleStarEffectsClientRpc(clientId, enabled);
    }

    public void OnSpawnJumpEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnJumpParticles();

        SpawnJumpEffectsClientRpc(clientId);
    }

    public void OnSpawnStunEffectsOnPlayer(ulong clientId, int milliseconds)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnStunParticles(milliseconds);

        SpawnStunEffectsClientRpc(clientId, milliseconds);
    }

    public void OnSpawnConfettiEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        //if (localVFXManager != null) localVFXManager.SpawnConfettiParticles();

        SpawnConfettiEffectsClientRpc(clientId);
    }

    public void OnSpawnBatConfusionEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnBatConfusionParticles();

        SpawnBatConfusionEffectsClientRpc(clientId);
    }

    public void OnRemoveBatConfusionEffectsOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.RemoveBatConfusionParticles();

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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
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
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveBatConfusionEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnBatConfusionParticles();
        }
        else
        {
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
        }
    }

    private PlayerVisualEffectManager GetPlayerEffectManagerById(ulong id) => NetworkManager.Singleton.ConnectedClients[id]?.PlayerObject?.GetComponent<PlayerVisualEffectManager>();
    private PlayerVisualEffectManager GetFirstValidEffectManager() => NetworkManager.Singleton.ConnectedClients.Values.First(p => p.PlayerObject != null).PlayerObject.GetComponent<PlayerVisualEffectManager>();
    
}

