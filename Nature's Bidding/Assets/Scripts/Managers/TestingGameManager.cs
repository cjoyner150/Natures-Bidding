using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine.Events;
using UnityUtils;
using System.Collections;

public class TestingGameManager : Singleton<TestingGameManager>
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    private int nextSpawnIndex = 0;

    [SerializeField] private GameObject playerHealthBarPrefab;
    [SerializeField] private Transform playerHealthBarParent;

    void OnEnable()
    {
        StartCoroutine(WaitForNetworkManager());
    }

    IEnumerator WaitForNetworkManager()
    {
        while (NetworkManager.Singleton == null)
            yield return null;

        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        Debug.Log("ApprovalCheck registered successfully.");
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log($"ApprovalCheck: spawnPoints count = {spawnPoints?.Count ?? -1}");

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            response.Approved = false;
            response.Reason = "No spawn points available";
            return;
        }
        
        Transform spawnPoint = spawnPoints[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;
        
        response.Approved = true;
        response.CreatePlayerObject = true; 
        response.Position = spawnPoint.position; 
        response.Rotation = spawnPoint.rotation; 
    }

    public PlayerGameplayUI SpawnPlayerHealthBar()
    {
        GameObject healthGO = Instantiate(playerHealthBarPrefab, playerHealthBarParent);
        
        PlayerGameplayUI gameplayUI = healthGO.GetComponent<PlayerGameplayUI>();
        
        return gameplayUI;
    }
    
}

