using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine.Events;
using UnityUtils;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameplaySpawnManager : Singleton<GameplaySpawnManager>
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    private int nextSpawnIndex = 0;

    [SerializeField] private GameObject playerHealthBarPrefab;
    [SerializeField] private Transform playerHealthBarParent;

    protected override void Awake()
    {
        if (HasInstance)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();

        ResolvePlayerPrefabFromNetworkManager();
    }

    public NetworkObject SpawnPlayer(ulong clientId)
    {
        RefreshSpawnPointsIfNeeded();
        ResolvePlayerPrefabFromNetworkManager();

        if (playerPrefab == null)
        {
            Debug.LogError("[GameplaySpawnManager] playerPrefab is not assigned.");
            return null;
        }

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[GameplaySpawnManager] No spawn points available in the active scene.");
            return null;
        }

        if (nextSpawnIndex >= spawnPoints.Count)
            nextSpawnIndex = 0;

        Transform spawnPoint = spawnPoints[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;

        if (spawnPoint == null)
        {
            Debug.LogError("[GameplaySpawnManager] Spawn point reference is missing.");
            return null;
        }

        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        return netObj;
    }

    public PlayerGameplayUI SpawnPlayerHealthBar()
    {
        if (playerHealthBarPrefab == null)
        {
            Debug.LogError("[GameplaySpawnManager] playerHealthBarPrefab is not assigned.");
            return null;
        }

        GameObject healthGO = Instantiate(playerHealthBarPrefab, playerHealthBarParent);
        
        PlayerGameplayUI gameplayUI = healthGO.GetComponent<PlayerGameplayUI>();
        
        return gameplayUI;
    }

    public bool HasConfiguredPlayerPrefab()
    {
        ResolvePlayerPrefabFromNetworkManager();
        return playerPrefab != null;
    }

    public void ResolvePlayerPrefabFromNetworkManager()
    {
        if (playerPrefab != null) return;
        if (NetworkManager.Singleton == null) return;

        playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        if (playerPrefab != null)
            Debug.Log("[GameplaySpawnManager] Using player prefab from NetworkManager configuration.");
    }

    private void RefreshSpawnPointsIfNeeded()
    {
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            bool hasLiveSpawnPoint = false;
            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint != null)
                {
                    hasLiveSpawnPoint = true;
                    break;
                }
            }

            if (hasLiveSpawnPoint) return;
        }

        GameObject spawnParent = GameObject.Find("SpawnPoints");
        if (spawnParent == null)
            return;

        spawnPoints = new List<Transform>();
        foreach (Transform child in spawnParent.transform)
            spawnPoints.Add(child);

        nextSpawnIndex = 0;
    }
    
}

