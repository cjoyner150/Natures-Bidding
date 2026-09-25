using Cysharp.Threading.Tasks;
using System;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityUtils;
using Random = UnityEngine.Random;
using Steamworks;

public class PersistentGameStateManager : Singleton<PersistentGameStateManager>
{
    public enum GameFlowPhase { Lobby, Map, Bidding, ShopReview, Combat }

    private const string BiddingSceneName = "Bidding_Scene";
    private const string VolcanoCombatSceneName = "LavaGameplay";
    private const string CliffsCombatSceneName = "CliffGameplay";
    private const string MapSceneName = "MapScene";

    [SerializeField] private GameObject[] spawnableNetworkSingletons; 
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingSlider;
    public GameObject LoadingPanel => loadingPanel;

    [SerializeField] TextMeshProUGUI loadingStatus;
    [SerializeField] TextMeshProUGUI loadingProgress;
    [SerializeField] private int combatWinsRequiredToEnd = 3;

    [Header("Debug")]
    [SerializeField] bool skipToCombat;

    [Header("Game Flow")]
    [SerializeField] private GameObject biddingCanvas;
    [SerializeField] private GameObject shopCanvas;

    [Header("Flow Managers")]
    [SerializeField] private BiddingManager biddingManager;
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ReadyManager readyManager;

    private GameAudioController gameAudioController;

    private bool _isReturningToMenu = false;
    public bool IsReturningToMenu {
        get => _isReturningToMenu;
        private set { _isReturningToMenu = value; }
    }

    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            loadingPanel.SetActive(value);
        }
    }

    public enum GameState {
        Menu,
        Lobby,
        Map,
        Bidding,
        Shopping,
        Combat
    }

    private GameState _state = GameState.Menu;
    public GameState State {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnGameStateChanged(value);
            }
        }
    }

    public enum CombatLevelSelectType
    {
        Cliffs,
        Volcano,
        Random
    }

    private CombatLevelSelectType levelSelectionType = CombatLevelSelectType.Random;

    // -1 means no seed generated yet for this run / no node chosen yet (floor 0 is open to vote on).
    private int currentMapSeed = -1;
    private int currentMapNodeId = -1;
    public int CurrentMapNodeId => currentMapNodeId;

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            InputDeviceTracker.Initialize();
            gameAudioController = GetComponent<GameAudioController>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            InputDeviceTracker.Shutdown();
        }
    }

    private void OnEnable()
    {
        LobbyServerHandler.OnAllPlayersReadied.AddListener(OnAllPlayersReadied);
        NetworkSessionManager.OnSessionHosted += OnSessionHosted;
    }

    private void OnDisable()
    {
        LobbyServerHandler.OnAllPlayersReadied.RemoveListener(OnAllPlayersReadied);
        NetworkSessionManager.OnSessionHosted -= OnSessionHosted;
    }

    private async void OnGameStateChanged(GameState newState)
    {
        gameAudioController ??= GetComponent<GameAudioController>();
        gameAudioController?.SetGameState(newState);

        switch (newState)
        {
            case GameState.Menu:
            case GameState.Map:
            case GameState.Bidding:
            case GameState.Shopping:
            case GameState.Combat:
                await NetworkSessionManager.Instance.SetSessionLocked(true);
                break;
            case GameState.Lobby:
                await NetworkSessionManager.Instance.SetSessionLocked(false);
                break;
        }

        PersistentSteamManager.Instance.UpdateRichPresence(newState);
    }

    public async UniTask LoadMenuScene()
    {
        SetLoadingState("Loading Menu...", true);

        await LoadSceneAsync(1);
    }

    public void SetLoadingProgress(float progress)
    {
        loadingProgress.text = $"{progress:F1}%";
        loadingSlider.value = progress / 100f;
    }

    public void SetLoadingState(string status, bool showProgress = false)
    {
        IsLoading = true;
        loadingStatus.text = status;
        loadingProgress.gameObject.SetActive(showProgress);
        if (!showProgress)
        {
            loadingProgress.text = "";
            loadingSlider.value = 0;
        }
    }

    public void ClearLoadingState()
    {
        IsLoading = false;
        loadingStatus.text = "";
        loadingProgress.text = "";
        loadingProgress.gameObject.SetActive(true);
        loadingSlider.value = 0f;
    }

    private void OnSessionHosted()
    {
        foreach (var prefab in spawnableNetworkSingletons)
        {
            var go = Instantiate(prefab);
            go.GetComponent<NetworkObject>().Spawn();
        }
        LoadLobbyLevel();
    }

    public async void LoadLobbyLevel()
    {
        SetLoadingState("Loading Lobby...");

        await LoadNetworkedSceneAsync("LobbyScene");
    }

    public async void LoadBiddingLevel()
    {
        SetLoadingState("Loading bidding...", true);

        State = GameState.Bidding;
        await LoadNetworkedSceneAsync(BiddingSceneName);
    }

    public async void LoadMapLevel()
    {
        SetLoadingState("Loading map...", true);

        State = GameState.Map;
        await LoadNetworkedSceneAsync(MapSceneName);
    }

    public async void OnLobbySceneReady()
    {
        SetLoadingState("Registering Data...");

        State = GameState.Lobby;

        await UniTask.WhenAny(
            UniTask.WaitUntil(() => PlayerRegistryNetworkSync.Instance != null && StatusEffectNetworkManager.Instance != null),
            UniTask.Delay(3000)
        );

        if (PlayerRegistryNetworkSync.Instance == null || StatusEffectNetworkManager.Instance == null)
            GameLogger.Log(LogSeverity.Warning, "Lobby bootstrap singletons were not ready in time; continuing anyway.");

        RegisterAuthData();

        SetLoadingState("Spawning...");

        await UniTask.WaitUntil(() => NetworkManager.Singleton.LocalClient.PlayerObject != null);

        ClearLoadingState();
    }

    public void OnBiddingSceneReady()
    {
        State = GameState.Bidding;
        ClearLoadingState();

        // The Lobby/Combat player-owned cursor is gone by now (destroyed with its scene); hide the map's stand-in cursor.
        CursorUIManager.Instance?.SetLocalCursorEnabled(false);
    }

    public void OnMapSceneReady()
    {
        State = GameState.Map;
        ClearLoadingState();

        // Map node clicks need a visible, unlocked cursor, but Lobby/Combat's player-owned cursor is gone by now (destroyed with its scene).
        CursorUIManager.Instance?.SetLocalCursorEnabled(true);
    }

    /// <summary>Server-only. Returns the seed for the current run's map, generating one the first time it's requested.</summary>
    public int RequestMapSeed()
    {
        if (currentMapSeed == -1)
            currentMapSeed = Random.Range(0, 999999);

        return currentMapSeed;
    }

    public void ConfigureGameFlowReferences(
        GameObject newBiddingCanvas,
        GameObject newShopCanvas,
        BiddingManager newBiddingManager,
        ShopManager newShopManager,
        ReadyManager newReadyManager)
    {
        GameLogger.Log(LogSeverity.Debug, "Configuring game flow references...");
        biddingCanvas = newBiddingCanvas;
        shopCanvas = newShopCanvas;
        biddingManager = newBiddingManager;
        shopManager = newShopManager;
        readyManager = newReadyManager;
    }

    public async UniTask InitializeBiddingFlowIfServer()
    {
        await UniTask.Yield();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            BeginBiddingPhaseServer();
    }

    public void SyncFlowPhase(GameFlowPhase phase)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            return;

        ApplyFlowPhase(phase);
    }

    public void RequestStartShopPhase()
    {

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            ShopManager.Instance?.StartShopPhaseRpc();
            return;
        }

        BeginShopPhaseServer();
    }

    public void RequestStartBiddingPhase()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            BiddingManager.Instance?.StartBiddingPhaseRpc();
            return;
        }

        BeginBiddingPhaseServer();
    }

    public void RequestStartCombatPhase()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            ReadyManager.Instance?.StartCombatPhaseRpc();
            return;
        }

        BeginCombatPhaseServer();
    }

    public void RequestReturnToMap()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            ReadyManager.Instance?.ReturnToMapRpc();
            return;
        }

        LoadMapLevel();
    }

    /// <summary>Server-only. Routes the flow based on which node type players voted for on the map.</summary>
    public void OnMapNodeSelected(int nodeId, NodeType nodeType)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        currentMapNodeId = nodeId;

        switch (nodeType)
        {
            case NodeType.Fight:
                BeginCombatPhaseServer();
                break;
            case NodeType.Shop:
                BeginBiddingPhaseServer();
                break;
            default:
                // Tarot/Clense/Curse nodes have no implementation yet — stub back to the map so the loop doesn't stall.
                GameLogger.Log(LogSeverity.Warning, $"Map node type {nodeType} is not implemented yet; returning to map.");
                LoadMapLevel();
                break;
        }
    }

    public void BeginBiddingPhaseServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        readyManager?.ResetForNewPhase();
        ApplyFlowPhase(GameFlowPhase.Bidding);
        biddingManager?.BeginBiddingPhase();
    }

    public void BeginShopPhaseServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        readyManager?.ResetForNewPhase();
        ApplyFlowPhase(GameFlowPhase.ShopReview);
    }

    public void BeginCombatPhaseServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        ApplyFlowPhase(GameFlowPhase.Combat);
        readyManager?.SyncCombatStateRpc();
        LoadCombatLevel();
    }



    public async UniTask ReturnToMenu()
    {
        GameLogger.Log(LogSeverity.Verbose, $"[ReturnToMenu] CALLED. Stack trace:\n{System.Environment.StackTrace}");
        if (IsReturningToMenu) return;
        IsReturningToMenu = true;

        SetLoadingState("Leaving session...");

        PersistentPlayerRegistry.Instance.Clear();
        State = GameState.Menu;
        currentMapSeed = -1;
        currentMapNodeId = -1;

        _sceneLoadTcs?.TrySetCanceled();
        _sceneLoadTcs = null;

        if (NetworkSessionManager.Instance.HasActiveSession)
            await NetworkSessionManager.Instance.LeaveSession();

        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening
        );

        Cursor.lockState = CursorLockMode.Confined;

        SetLoadingState("Returning to Menu...", true);

        await LoadSceneAsync(1);

        await UniTask.WaitUntil(() => NetworkManager.Singleton != null);
        IsReturningToMenu = false;

        ClearLoadingState();
    }

    private async UniTask LoadSceneAsync(int idx)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            await LoadNetworkedSceneAsync(idx);
        }
        else
        {
            await LoadStandaloneSceneAsync(idx);
        }
    }

    private UniTaskCompletionSource _sceneLoadTcs;

    private async UniTask LoadNetworkedSceneAsync(int idx)
    {
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(
            SceneUtility.GetScenePathByBuildIndex(idx)
        );

        await LoadNetworkedSceneAsync(sceneName);
    }

    private async UniTask LoadNetworkedSceneAsync(string sceneName)
    {
        GameLogger.Log(LogSeverity.Debug, $"LoadNetworkedSceneAsync. IsServer: {NetworkManager.Singleton.IsServer}, IsListening: {NetworkManager.Singleton.IsListening}");

        _sceneLoadTcs = new UniTaskCompletionSource();

        GameLogger.Log(LogSeverity.Debug, $"Loading scene: {sceneName}");

        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        if (NetworkManager.Singleton.IsServer)
        {
            GameLogger.Log(LogSeverity.Debug, "IsServer calling LoadScene.");
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            GameLogger.Log(LogSeverity.Debug, "Not server waiting for scene sync from server.");
        }

        try
        {
            await _sceneLoadTcs.Task;
        }
        catch (OperationCanceledException e)
        {
            GameLogger.LogException(LogSeverity.Warning, "Scene load cancelled.", e);
        }
        finally
        {
            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        GameLogger.Log(LogSeverity.Debug, $"SceneEvent: {sceneEvent.SceneEventType}, ClientId: {sceneEvent.ClientId}, Local: {NetworkManager.Singleton.LocalClientId}");

        if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId) return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load:
                if (sceneEvent.AsyncOperation != null)
                    TrackLoadProgress(sceneEvent.AsyncOperation).Forget();
                break;

            case SceneEventType.LoadComplete:
            case SceneEventType.SynchronizeComplete:
                SetLoadingProgress(100);
                _sceneLoadTcs?.TrySetResult();
                break;
        }
    }

    private async UniTaskVoid TrackLoadProgress(AsyncOperation op)
    {
        while (op.progress < .9f)
        {
            SetLoadingProgress(Mathf.Clamp(op.progress / 0.9f * 100f, 0f, 100f));
            await UniTask.Yield();
        }
    }

    private async UniTask LoadStandaloneSceneAsync(int idx)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(idx);
        op.allowSceneActivation = false;

        TrackLoadProgress(op).Forget();

        await UniTask.WaitUntil(() => op.progress >= .9f);

        SetLoadingProgress(100);

        await UniTask.Delay(200);
        op.allowSceneActivation = true;

        await UniTask.WaitUntil(() => op.isDone);
    }

    public async void RegisterAuthData()
    {
        await UniTask.WaitUntil(() =>
        {
            IGameServerHandler handler = FindAnyObjectByType<LobbyServerHandler>();
            handler ??= FindAnyObjectByType<CombatServerHandler>();
            return handler != null;
        });

        string playerId = AuthenticationService.Instance.PlayerId ?? "unknown";

#if UNITY_EDITOR
        string playerName = "EditorPlayer";
#else
    string playerName = SteamClient.Name;
    if (string.IsNullOrWhiteSpace(playerName)) playerName = "Player";
#endif

        if (playerName.Length > 24) playerName = playerName.Substring(0, 24);

        if (LobbyServerHandler.Instance != null)
            LobbyServerHandler.Instance.SendAuthToServerRpc(playerId, playerName);
        else if (CombatServerHandler.Instance != null)
            CombatServerHandler.Instance.SendAuthToServerRpc(playerId, playerName);
    }

    private void OnAllPlayersReadied()
    {
        StartNewRound();
    }

    private void StartNewRound()
    {
        if (skipToCombat)
        {
            BeginCombatPhaseServer();
        }
        else
        {
            LoadMapLevel();
        }
    }

    private void ApplyFlowPhase(GameFlowPhase phase)
    {
        if (biddingCanvas == null || shopCanvas == null)
        {
            GameLogger.Log(LogSeverity.Warning, $"ApplyFlowPhase({phase}) called before canvases were configured. Deferring.");
            WaitForCanvasesThenApply(phase).Forget();
            return;
        }

        ApplyFlowPhaseInternal(phase);
    }

    private async UniTaskVoid WaitForCanvasesThenApply(GameFlowPhase phase)
    {
        await UniTask.WaitUntil(() => biddingCanvas != null && shopCanvas != null);
        ApplyFlowPhaseInternal(phase);
    }

    private void ApplyFlowPhaseInternal(GameFlowPhase phase)
    {
        switch (phase)
        {
            case GameFlowPhase.Lobby: State = GameState.Lobby; break;
            case GameFlowPhase.Bidding: State = GameState.Bidding; break;
            case GameFlowPhase.ShopReview: State = GameState.Shopping; break;
            case GameFlowPhase.Combat: State = GameState.Combat; break;
        }

        biddingCanvas.SetActive(phase == GameFlowPhase.Bidding);
        shopCanvas.SetActive(phase == GameFlowPhase.ShopReview);

        if (phase == GameFlowPhase.ShopReview)
            PointerNPC.Instance?.HideSpeechBubble();

        switch (phase)
        {
            case GameFlowPhase.Bidding:
                biddingManager?.OnBiddingPhaseStart();
                break;
            case GameFlowPhase.ShopReview:
                shopManager?.OnShopPhaseStart();
                break;
            case GameFlowPhase.Combat:
                biddingCanvas.SetActive(false);
                shopCanvas.SetActive(false);
                break;
        }
    }

    public async void LoadCombatLevel()
    {
        SetLoadingState("Loading combat...", true);
        string sceneName;

        switch (levelSelectionType)
        {
            case CombatLevelSelectType.Cliffs:
                sceneName = CliffsCombatSceneName;
                break;
            case CombatLevelSelectType.Volcano:
                sceneName = VolcanoCombatSceneName;
                break;
            case CombatLevelSelectType.Random:
                int rand = Random.Range(0, 2);
                var level = (CombatLevelSelectType)rand;
                if (level == CombatLevelSelectType.Cliffs) sceneName = CliffsCombatSceneName;
                else if (level == CombatLevelSelectType.Volcano) sceneName = VolcanoCombatSceneName;
                else
                {
                    GameLogger.Log(LogSeverity.Error, "Random level type generated unimplemented level. Defaulting to cliffs level.");
                    sceneName = CliffsCombatSceneName;
                }
                break;
            default:
                GameLogger.Log(LogSeverity.Error, "LevelSelectionType is set to an unimplemented level. Defaulting to cliffs level.");
                sceneName = CliffsCombatSceneName;
                break;
        }

        await LoadNetworkedSceneAsync(sceneName);
    }

    public void OnCombatSceneReady()
    {
        ClearLoadingState();
        State = GameState.Combat;

        // Combat spawns its own fresh player-owned cursor; hide the map's stand-in cursor.
        CursorUIManager.Instance?.SetLocalCursorEnabled(false);
    }

    public async UniTask HandleCombatRoundEnded(ulong winningPlayerId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        PersistentPlayerRegistry.Instance.AddCombatWin(winningPlayerId);
        PersistentPlayerRegistry.Instance.AddGold(winningPlayerId, 150);

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == winningPlayerId) continue;

            PersistentPlayerRegistry.Instance.AddGold(clientId, 300);
        }

        PlayerData winningPlayer = PersistentPlayerRegistry.Instance.GetByClientId(winningPlayerId);
        if (winningPlayer != null && winningPlayer.combatWins >= combatWinsRequiredToEnd)
        {
            State = GameState.Menu;
            await ReturnToMenu();
            return;
        }

        StartNewRound();
    }

    public void SetLevelSelectionType(CombatLevelSelectType level) { levelSelectionType = level; }

    public CombatLevelSelectType GetCurrentLevelSelectionType() { return levelSelectionType; }
}
