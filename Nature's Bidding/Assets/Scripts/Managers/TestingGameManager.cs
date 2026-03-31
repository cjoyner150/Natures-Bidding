using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine.Events;
using UnityUtils;

public class TestingGameManager : Singleton<TestingGameManager>
{

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints;
    private int nextSpawnIndex = 0;

    [SerializeField] private GameObject playerHealthBarPrefab;
    [SerializeField] private Transform playerHealthBarParent;

    public static UnityEvent OnSessionStarted = new UnityEvent();

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

    [ContextMenu("TestBeginSession")]
    public void BeginSession()
    {
        BeginSessionAllRpc();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    void BeginSessionAllRpc()
    {
        OnSessionStarted?.Invoke();
    }

    public PlayerGameplayUI SpawnPlayerHealthBar()
    {
        GameObject healthGO = Instantiate(playerHealthBarPrefab, playerHealthBarParent);
        
        PlayerGameplayUI gameplayUI = healthGO.GetComponent<PlayerGameplayUI>();
        
        return gameplayUI;
    }
    
}

