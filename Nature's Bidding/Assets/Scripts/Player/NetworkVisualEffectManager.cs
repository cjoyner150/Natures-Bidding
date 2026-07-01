using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkVisualEffectManager : NetworkSingleton<NetworkVisualEffectManager>
{

    // Locally call event from anywhere in normal code with the clientId
    public static Action<ulong> SpawnHitReactionEffectsOnPlayer;
    public static Action<Vector3> SpawnExplosionAtPosition;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        DontDestroyOnLoad(gameObject);

        SpawnHitReactionEffectsOnPlayer += OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition += OnSpawnExplosionAtPosition;
    }

    public override void OnNetworkDespawn()
    {
        SpawnHitReactionEffectsOnPlayer -= OnSpawnHitReactionEffectsOnPlayer;
        SpawnExplosionAtPosition -= OnSpawnExplosionAtPosition;
    }

    public void OnSpawnExplosionAtPosition(Vector3 spawnPos) => SpawnExplosionAtPositionClientRpc(spawnPos);
    public void OnSpawnHitReactionEffectsOnPlayer(ulong clientId) => SpawnHitReactionEffectsClientRpc(clientId);


    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Everyone)]
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

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnHitReactionEffectsClientRpc(ulong clientId)
    {
        var playerEffectManager = GetPlayerEffectManagerById(clientId);

        if (playerEffectManager != null)
        {
            playerEffectManager.SpawnHitReactParticles();
        }
        else
        {
            Debug.LogError("[NetworkVisualEffectManager] Player Visual Effect Manager not found.");
        }
    }

    private PlayerVisualEffectManager GetPlayerEffectManagerById(ulong id) => NetworkManager.Singleton.ConnectedClients[id]?.PlayerObject?.GetComponent<PlayerVisualEffectManager>();
    private PlayerVisualEffectManager GetFirstValidEffectManager() => NetworkManager.Singleton.ConnectedClients.Values.First(p => p.PlayerObject != null).PlayerObject.GetComponent<PlayerVisualEffectManager>();
    
}

