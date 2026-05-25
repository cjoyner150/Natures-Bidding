using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// InventoryScreenManager — Manages the inventory screen shown after the shop.
/// Spawns one PlayerInventoryPanel per player in the same 2x2 grid layout.
/// Attach to a NetworkObject in the scene.
/// </summary>
public class InventoryScreenManager : NetworkBehaviour
{
    public static InventoryScreenManager Instance { get; private set; }

    #region Inspector Fields

    [Header("Layout")]
    public Transform  panelsContainer;          // Grid Layout Group — same setup as shop
    public GameObject playerInventoryPanelPrefab;

    #endregion

    #region Private

    private List<PlayerInventoryPanel> _panels = new List<PlayerInventoryPanel>();

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #endregion

    #region Phase Start

    /// <summary>Called by GameFlowManager when inventory phase begins.</summary>
    public void OnInventoryPhaseStart()
    {
        BuildPanelsRpc();
    }

    [Rpc(SendTo.Everyone)]
    void BuildPanelsRpc()
    {
        // Destroy old panels
        foreach (var p in _panels) { if (p) Destroy(p.gameObject); }
        _panels.Clear();

        if (playerInventoryPanelPrefab == null)
        {
            Debug.LogError("[InventoryScreenManager] playerInventoryPanelPrefab not assigned!");
            return;
        }

        ulong localId = NetworkManager.Singleton.LocalClientId;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            var go    = Instantiate(playerInventoryPanelPrefab, panelsContainer);
            var panel = go.GetComponent<PlayerInventoryPanel>();
            if (panel == null)
            {
                Debug.LogError("[InventoryScreenManager] prefab is missing PlayerInventoryPanel component!");
                continue;
            }

            bool isLocal = kvp.Key == localId;
            panel.Initialise(kvp.Key, isLocal);
            _panels.Add(panel);
        }
    }

    #endregion
}