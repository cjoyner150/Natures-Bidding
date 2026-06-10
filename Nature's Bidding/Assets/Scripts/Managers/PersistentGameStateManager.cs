using Cysharp.Threading.Tasks;
using System;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityUtils;

public class PersistentGameStateManager : Singleton<PersistentGameStateManager>
{
    private const string BiddingSceneName = "Bidding_Scene";
    private const string CombatSceneName = "CliffGameplay";

    [SerializeField] private GameObject loadingPanel;
    public GameObject LoadingPanel => loadingPanel;

    [SerializeField] TextMeshProUGUI loadingStatus;
    [SerializeField] TextMeshProUGUI loadingProgress;
    [SerializeField] private int combatWinsRequiredToEnd = 3;

    [Header("Debug")]
    [SerializeField] bool skipToCombat;


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
    }

    public void SetLoadingState(string status, bool showProgress = false)
    {
        IsLoading = true;
        loadingStatus.text = status;
        loadingProgress.gameObject.SetActive(showProgress);
        if (!showProgress)
            loadingProgress.text = "";
    }

    public void ClearLoadingState()
    {
        IsLoading = false;
        loadingStatus.text = "";
        loadingProgress.text = "";
        loadingProgress.gameObject.SetActive(true);
    }

    private void OnSessionHosted()
    {
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
        SetLoadingState("Spawning...");

        State = GameState.Lobby;
        RegisterAuthData();

        await UniTask.WaitUntil(() => NetworkManager.Singleton.LocalClient.PlayerObject != null);

        ClearLoadingState();
    }

    public void OnBiddingSceneReady()
    {
        State = GameState.Bidding;
        ClearLoadingState();
    }


    public async UniTask ReturnToMenu()
    {
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
            LoadCombatLevel();
        }
        else
        {
            LoadBiddingLevel();
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

        PersistentPlayerData winningPlayer = PersistentPlayerRegistry.Instance.GetByClientId(winningPlayerId);
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
