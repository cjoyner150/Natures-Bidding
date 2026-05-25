using Unity.Netcode;
using UnityEngine;
using System.Collections;

/// <summary>
/// GameFlowManager — Server-authoritative phase state machine.
/// Phases: Lobby → Bidding → Shop → Inventory → Bidding → ...
/// </summary>
public class GameFlowManager : NetworkBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public enum GamePhase { Lobby, Bidding, ShopReview, Inventory }

    #region Inspector Fields

    [Header("Phase Canvases")]
    public GameObject biddingCanvas;
    public GameObject shopCanvas;
    public GameObject inventoryCanvas;

    [Header("Managers")]
    public BiddingManager        biddingManager;
    public ShopManager           shopManager;
    public InventoryScreenManager inventoryScreenManager;
    public ReadyManager          readyManager;

    #endregion

    #region Network Variables

    public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(
        GamePhase.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        ApplyPhase(CurrentPhase.Value);

        if (IsServer)
            StartCoroutine(BeginAfterSpawn());
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
    }

    IEnumerator BeginAfterSpawn()
    {
        yield return null;
        BeginBiddingPhase();
    }

    #endregion

    #region Phase Entry Points (server only)

    void BeginBiddingPhase()
    {
        if (!IsServer) return;
        readyManager?.ResetForNewPhase();
        CurrentPhase.Value = GamePhase.Bidding;
        biddingManager?.BeginBiddingPhase();
    }

    void BeginShopPhase()
    {
        if (!IsServer) return;
        readyManager?.ResetForNewPhase();
        CurrentPhase.Value = GamePhase.ShopReview;
        shopManager?.OnShopPhaseStart();
    }

    void BeginInventoryPhase()
    {
        if (!IsServer) return;
        CurrentPhase.Value = GamePhase.Inventory;
        inventoryScreenManager?.OnInventoryPhaseStart();
    }

    #endregion

    #region Public RPCs

    [Rpc(SendTo.Server)]
    public void StartShopPhaseRpc()
    {
        BeginShopPhase();
    }

    [Rpc(SendTo.Server)]
    public void StartBiddingPhaseRpc()
    {
        BeginBiddingPhase();
    }

    [Rpc(SendTo.Server)]
    public void StartInventoryPhaseRpc()
    {
        BeginInventoryPhase();
    }

    #endregion

    #region Phase Transitions

    void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase) => ApplyPhase(newPhase);

    void ApplyPhase(GamePhase phase)
    {
        if (biddingCanvas)   biddingCanvas.SetActive(phase == GamePhase.Bidding);
        if (shopCanvas)      shopCanvas.SetActive(phase == GamePhase.ShopReview);
        if (inventoryCanvas) inventoryCanvas.SetActive(phase == GamePhase.Inventory);

        switch (phase)
        {
            case GamePhase.Bidding:
                biddingManager?.OnBiddingPhaseStart();
                break;
            case GamePhase.ShopReview:
                shopManager?.OnShopPhaseStart();
                break;
            case GamePhase.Inventory:
                inventoryScreenManager?.OnInventoryPhaseStart();
                break;
        }
    }

    #endregion
}