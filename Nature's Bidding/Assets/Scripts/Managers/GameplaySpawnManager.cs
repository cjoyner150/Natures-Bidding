using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine.Events;
using UnityUtils;
using System.Collections;

public class GameplaySpawnManager : Singleton<GameplaySpawnManager>
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    private int nextSpawnIndex = 0;

    [SerializeField] private GameObject playerHealthBarPrefab;
    [SerializeField] private Transform playerHealthBarParent;

    public NetworkObject SpawnPlayer(ulong clientId)
    {
        Transform spawnPoint = spawnPoints[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;

        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        return netObj;
    }

    public PlayerGameplayUI SpawnPlayerHealthBar()
    {
        GameObject healthGO = Instantiate(playerHealthBarPrefab, playerHealthBarParent);
        
        PlayerGameplayUI gameplayUI = healthGO.GetComponent<PlayerGameplayUI>();
        
        return gameplayUI;
    }
    
}

