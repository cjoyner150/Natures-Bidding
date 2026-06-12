using Unity.Netcode;
using UnityEngine;

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

        PersistentGameStateManager.Instance?.ConfigureGameFlowReferences(
            biddingCanvas,
            shopCanvas,
            biddingManager,
            shopManager,
            readyManager);
    }

    public override void OnNetworkSpawn()
    {
        PersistentGameStateManager.Instance?.OnBiddingSceneReady();
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        PersistentGameStateManager.Instance?.SyncFlowPhase(ToPersistentPhase(CurrentPhase.Value));

        if (IsServer)
            PersistentGameStateManager.Instance?.InitializeBiddingFlowIfServer().Forget();
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;
    }

    #endregion

    #region Phase Entry Points (server only)

    void BeginBiddingPhase()
    {
        if (!IsServer) return;
        CurrentPhase.Value = GamePhase.Bidding;
        PersistentGameStateManager.Instance?.BeginBiddingPhaseServer();
    }

    void BeginShopPhase()
    {
        if (!IsServer) return;
        CurrentPhase.Value = GamePhase.ShopReview;
        PersistentGameStateManager.Instance?.BeginShopPhaseServer();
    }

    void BeginCombatPhase()
    {
        if (!IsServer) return;
        CurrentPhase.Value = GamePhase.Combat;
        PersistentGameStateManager.Instance?.BeginCombatPhaseServer();
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

    void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
    {
        PersistentGameStateManager.Instance?.SyncFlowPhase(ToPersistentPhase(newPhase));
    }

    private static PersistentGameStateManager.GameFlowPhase ToPersistentPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Lobby:
                return PersistentGameStateManager.GameFlowPhase.Lobby;
            case GamePhase.Bidding:
                return PersistentGameStateManager.GameFlowPhase.Bidding;
            case GamePhase.ShopReview:
                return PersistentGameStateManager.GameFlowPhase.ShopReview;
            case GamePhase.Combat:
                return PersistentGameStateManager.GameFlowPhase.Combat;
            default:
                return PersistentGameStateManager.GameFlowPhase.Lobby;
        }
    }

    #endregion
}