using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkVisualEffectManager : NetworkSingleton<NetworkVisualEffectManager>
{

    // Locally call event from anywhere in normal code with the clientId
    public static Action<ulong> SpawnSlashEffectsOnPlayer;

    public static Action<ulong, bool> SpawnHitReactionEffectsOnPlayer;
    public static Action<Vector3> SpawnExplosionAtPosition;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);

        SpawnSlashEffectsOnPlayer += OnSpawnSlashEffectOnPlayer;
        SpawnHitReactionEffectsOnPlayer += OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition += OnSpawnExplosionAtPosition;
    }

    public override void OnNetworkDespawn()
    {
        SpawnSlashEffectsOnPlayer -= OnSpawnSlashEffectOnPlayer;
        SpawnHitReactionEffectsOnPlayer -= OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition -= OnSpawnExplosionAtPosition;
    }

    public void OnSpawnExplosionAtPosition(Vector3 spawnPos) 
    {
        var localVFXManager = GetFirstValidEffectManager();
        if (localVFXManager != null) localVFXManager.SpawnExplosionParticles(spawnPos);

        SpawnExplosionAtPositionClientRpc(spawnPos);
    }
    public void OnSpawnHitReactionEffectsOnPlayer(ulong clientId, bool critical)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnHitReactParticles(critical);

        SpawnHitReactionEffectsClientRpc(clientId, critical);
    }

    public void OnSpawnSlashEffectOnPlayer(ulong clientId)
    {
        var localVFXManager = GetPlayerEffectManagerById(NetworkManager.Singleton.LocalClientId);
        if (localVFXManager != null) localVFXManager.SpawnSlashEffectParticles();

        SpawnSlashEffectClientRpc(clientId);
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
    public void SpawnHitReactionEffectsClientRpc(ulong clientId, bool critical)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnHitReactParticles(critical);
        }
        else
        {
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
        }
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnSlashEffectClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnSlashEffectParticles();
        }
        else
        {
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
        }
    }

    private PlayerVisualEffectManager GetPlayerEffectManagerById(ulong id) => NetworkManager.Singleton.ConnectedClients[id]?.PlayerObject?.GetComponent<PlayerVisualEffectManager>();
    private PlayerVisualEffectManager GetFirstValidEffectManager() => NetworkManager.Singleton.ConnectedClients.Values.First(p => p.PlayerObject != null).PlayerObject.GetComponent<PlayerVisualEffectManager>();
    
}

