using Cysharp.Threading.Tasks;
using System;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityUtils;

public class PersistentGameStateManager : Singleton<PersistentGameStateManager>
{
    public enum GameFlowPhase { Lobby, Bidding, ShopReview, Combat }

    private const string BiddingSceneName = "Bidding_Scene";
    private const string CombatSceneName = "LavaGameplay";

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

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
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
        switch (newState)
        {
            case GameState.Menu:
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

    public async void OnLobbySceneReady()
    {
        SetLoadingState("Registering Data...");

        State = GameState.Lobby;

        await UniTask.WhenAny(
            UniTask.WaitUntil(() => PlayerRegistryNetworkSync.Instance != null && StatusEffectNetworkManager.Instance != null),
            UniTask.Delay(3000)
        );

        if (PlayerRegistryNetworkSync.Instance == null || StatusEffectNetworkManager.Instance == null)
            Debug.LogWarning("[PersistentGameStateManager] Lobby bootstrap singletons were not ready in time; continuing anyway.");

        RegisterAuthData();

        SetLoadingState("Spawning...");

        await UniTask.WaitUntil(() => NetworkManager.Singleton.LocalClient.PlayerObject != null);

        ClearLoadingState();
    }

    public void OnBiddingSceneReady()
    {
        State = GameState.Bidding;
        ClearLoadingState();
    }

    public void ConfigureGameFlowReferences(
        GameObject newBiddingCanvas,
        GameObject newShopCanvas,
        BiddingManager newBiddingManager,
        ShopManager newShopManager,
        ReadyManager newReadyManager)
    {
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
        shopManager?.OnShopPhaseStart();
    }

    public void BeginCombatPhaseServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        ApplyFlowPhase(GameFlowPhase.Combat);
        LoadCombatLevel();
    }


    public async UniTask ReturnToMenu()
    {
        Debug.Log($"[ReturnToMenu] CALLED. Stack trace:\n{System.Environment.StackTrace}");
        if (IsReturningToMenu) return;
        IsReturningToMenu = true;

        SetLoadingState("Leaving session...");

        PersistentPlayerRegistry.Instance.Clear();
        State = GameState.Menu;

        _sceneLoadTcs?.TrySetCanceled();
        _sceneLoadTcs = null;

        if (NetworkSessionManager.Instance.HasActiveSession)
            await NetworkSessionManager.Instance.LeaveSession();

        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening
        );

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
        Debug.Log($"LoadNetworkedSceneAsync. IsServer: {NetworkManager.Singleton.IsServer}, IsListening: {NetworkManager.Singleton.IsListening}");

        _sceneLoadTcs = new UniTaskCompletionSource();

        Debug.Log($"Loading scene: {sceneName}");

        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("IsServer � calling LoadScene.");
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.Log("Not server � waiting for scene sync from server.");
        }

        try
        {
            await _sceneLoadTcs.Task;
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Scene load cancelled.");
        }
        finally
        {
            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log($"SceneEvent: {sceneEvent.SceneEventType}, ClientId: {sceneEvent.ClientId}, Local: {NetworkManager.Singleton.LocalClientId}");

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
        string playerName = AuthenticationService.Instance.PlayerName ?? "Player";

        if (LobbyServerHandler.Instance != null)
            LobbyServerHandler.Instance.SendAuthToServerRpc(playerId, playerName);
        else if (CombatServerHandler.Instance != null)
            CombatServerHandler.Instance.SendAuthToServerRpc(playerId, playerName);
    }

    private void OnAllPlayersReadied()
    {
        if (skipToCombat)
        {
            BeginCombatPhaseServer();
        }
        else
        {
            LoadBiddingLevel();
        }
    }

    private void ApplyFlowPhase(GameFlowPhase phase)
    {
        switch (phase)
        {
            case GameFlowPhase.Lobby:
                State = GameState.Lobby;
                break;
            case GameFlowPhase.Bidding:
                State = GameState.Bidding;
                break;
            case GameFlowPhase.ShopReview:
                State = GameState.Shopping;
                break;
            case GameFlowPhase.Combat:
                State = GameState.Combat;
                break;
        }

        // These checks have to be explicit because an empty serialized reference can be null but not equal to null,
        // which causes a NullReferenceException when trying to call SetActive on it.
        if (biddingCanvas != null)
        {
            biddingCanvas?.SetActive(phase == GameFlowPhase.Bidding);
        }
        else return;

        if (shopCanvas != null)
        {
            shopCanvas?.SetActive(phase == GameFlowPhase.ShopReview);
        }
        else return;

        if (phase == GameFlowPhase.ShopReview)
            PointerNPC.Instance?.HideSpeechBubble();

        //if (CursorManager.Instance != null)
        //{
        //    CursorManager.Instance.cursorEnabled =
        //        phase == GameFlowPhase.Bidding || phase == GameFlowPhase.ShopReview;
        //    Cursor.visible = CursorManager.Instance.cursorEnabled;
        //}

        switch (phase)
        {
            case GameFlowPhase.Bidding:
                biddingManager?.OnBiddingPhaseStart();
                break;
            case GameFlowPhase.ShopReview:
                shopManager?.OnShopPhaseStart();
                break;
            case GameFlowPhase.Combat:
                biddingCanvas?.SetActive(false);
                shopCanvas?.SetActive(false);
                break;
        }
    }

    public async void LoadCombatLevel()
    {
        SetLoadingState("Loading combat...", true);
        await LoadNetworkedSceneAsync(CombatSceneName);
    }

    public void OnCombatSceneReady()
    {
        ClearLoadingState();
        State = GameState.Combat;
    }

    public async UniTask HandleCombatRoundEnded(ulong winningPlayerId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        PersistentPlayerRegistry.Instance.AddCombatWin(winningPlayerId);

        PersistentPlayerState winningPlayer = PersistentPlayerRegistry.Instance.GetByClientId(winningPlayerId);
        if (winningPlayer != null && winningPlayer.combatWins >= combatWinsRequiredToEnd)
        {
            State = GameState.Menu;
            await ReturnToMenu();
            return;
        }

        State = GameState.Bidding;
        LoadBiddingLevel();
    }
}
