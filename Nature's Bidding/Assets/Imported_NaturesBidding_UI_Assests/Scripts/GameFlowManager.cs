using Unity.Netcode;
using UnityEngine;
using System.Collections;

/// <summary>
/// GameFlowManager — Server-authoritative phase state machine.
/// Phases: Lobby → Bidding → Shop → Combat → Bidding → ...
/// </summary>
public class GameFlowManager : NetworkBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public enum GamePhase { Lobby, Bidding, ShopReview, Combat }

    #region Inspector Fields

    [Header("Phase Canvases")]
    public GameObject biddingCanvas;
    public GameObject shopCanvas;

    [Header("Managers")]
    public BiddingManager        biddingManager;
    public ShopManager           shopManager;
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
        PersistentGameStateManager.Instance?.OnBiddingSceneReady();
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

    void BeginCombatPhase()
    {
        if (!IsServer) return;
        CurrentPhase.Value = GamePhase.Combat;
        PersistentGameStateManager.Instance?.LoadCombatLevel();
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
    public void StartCombatPhaseRpc()
    {
        BeginCombatPhase();
    }

    #endregion

    #region Phase Transitions

    void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase) => ApplyPhase(newPhase);

    void ApplyPhase(GamePhase phase)
    {
        if (biddingCanvas)   biddingCanvas.SetActive(phase == GamePhase.Bidding);
        if (shopCanvas)      shopCanvas.SetActive(phase == GamePhase.ShopReview);

        if (CursorUIManager.Instance != null)
        {
            CursorUIManager.Instance.cursorEnabled = phase == GamePhase.Bidding || phase == GamePhase.ShopReview;
            Cursor.visible = CursorUIManager.Instance.cursorEnabled;
        }

        switch (phase)
        {
            case GamePhase.Bidding:
                biddingManager?.OnBiddingPhaseStart();
                break;
            case GamePhase.ShopReview:
                shopManager?.OnShopPhaseStart();
                break;
            case GamePhase.Combat:
                biddingCanvas?.SetActive(false);
                shopCanvas?.SetActive(false);
                break;
        }
    }

    #endregion
}