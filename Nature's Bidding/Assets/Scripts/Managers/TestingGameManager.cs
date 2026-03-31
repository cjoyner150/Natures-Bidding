using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class TestingGameManager : MonoBehaviour
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    private int nextSpawnIndex = 0;

    void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
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
}

