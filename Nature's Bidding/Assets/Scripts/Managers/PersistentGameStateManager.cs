using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityUtils;

public class PersistentGameStateManager : Singleton<PersistentGameStateManager>
{
    [SerializeField] private GameObject loadingPanel;
    public GameObject LoadingPanel => loadingPanel;

    [SerializeField] TextMeshProUGUI loadingProgress;

    public enum GameState {
        Menu,
        Lobby,
        Bidding,
        Shopping,
        Combat
    }

    public GameState state = GameState.Menu;

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
        GameplayServerHandler.OnAllPlayersRegistered.AddListener(OnAllPlayersRegistered);
    }

    private void OnDisable()
    {
        GameplayServerHandler.OnAllPlayersRegistered.RemoveListener(OnAllPlayersRegistered);
    }

    public async void LoadGameplayLevel()
    {
        loadingPanel.SetActive(true);
        await LoadNetworkedSceneAsync(1);
        RegisterAuthData();
        loadingPanel.SetActive(false);
        state = GameState.Lobby;
    }

    public void OnGameplaySceneReady()
    {
        loadingPanel.SetActive(false);
        state = GameState.Lobby;
        RegisterAuthData();
    }

    private UniTaskCompletionSource _clientSyncTcs;

    public async UniTask WaitForClientSceneSync()
    {
        _clientSyncTcs = new UniTaskCompletionSource();
        await _clientSyncTcs.Task;
    }

    public void CompleteClientSceneSync()
    {
        _clientSyncTcs?.TrySetResult();
    }

    public async void ReturnToMenu()
    {
        PlayerRegistry.Instance.Clear();
        state = GameState.Menu;

        if (NetworkManager.Singleton != null)
        {
            GameObject networkManagerGO = NetworkManager.Singleton.gameObject;
            NetworkManager.Singleton.Shutdown();
            Destroy(networkManagerGO);
        }

        await LoadSceneAsync(0);
    }

    private async UniTask LoadSceneAsync(int idx)
    {
        loadingPanel.SetActive(true);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            await LoadNetworkedSceneAsync(idx);
        }
        else
        {
            await LoadStandaloneSceneAsync(idx);
        }

        loadingPanel.SetActive(false);
    }

    private UniTaskCompletionSource _sceneLoadTcs;

    private async UniTask LoadNetworkedSceneAsync(int idx)
    {
        _sceneLoadTcs = new UniTaskCompletionSource();

        string sceneName = System.IO.Path.GetFileNameWithoutExtension(
            SceneUtility.GetScenePathByBuildIndex(idx)
        );

        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

        await _sceneLoadTcs.Task;

        NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
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
                loadingProgress.text = "100%";
                _sceneLoadTcs?.TrySetResult();
                break;
        }
    }

    private async UniTaskVoid TrackLoadProgress(AsyncOperation op)
    {
        while (!op.isDone)
        {
            loadingProgress.text = $"{(op.progress / 0.9f * 100f):F1}%";
            await UniTask.Yield();
        }
    }

    private async UniTask LoadStandaloneSceneAsync(int idx)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(idx);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            loadingProgress.text = $"{(op.progress * 100f / 0.9f):F1}%";
            await UniTask.Delay(25);
        }

        loadingProgress.text = "100%";
        await UniTask.Delay(200);
        op.allowSceneActivation = true;
        await UniTask.WaitUntil(() => op.isDone);
    }

    public async void RegisterAuthData()
    {
        await UniTask.WaitUntil(() =>
            GameplayServerHandler.Instance != null &&
            LobbyNetworkMessenger.Instance != null &&
            LobbyNetworkMessenger.Instance.IsSpawned
        );

        PlayerRegistry.Instance.Register(
            NetworkManager.Singleton.LocalClientId,
            AuthenticationService.Instance.PlayerId,
            AuthenticationService.Instance.PlayerName
        );

        LobbyNetworkMessenger.Instance.SendAuthToServerRpc(
            AuthenticationService.Instance.PlayerId,
            AuthenticationService.Instance.PlayerName
        );
    }

    public void UpdateLoadingProgress(float percent)
    {
        loadingProgress.text = $"{percent:F1}%";
    }

    private void OnAllPlayersRegistered()
    {
        state = GameState.Combat;
    }
}
