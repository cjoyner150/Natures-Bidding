using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AkGameObj))]
public sealed class GameAudioController : MonoBehaviour
{
    private const string CliffSceneName = "CliffGameplay";
    private const string LavaSceneName = "LavaGameplay";

    [Header("Music Events")]
    [SerializeField] private AK.Wwise.Event playMusicSystem;
    [SerializeField] private AK.Wwise.Event stopMusicSystem;

    [Header("Game_Phase States")]
    [SerializeField] private AK.Wwise.State phaseMenu;
    [SerializeField] private AK.Wwise.State phaseLobby;
    [SerializeField] private AK.Wwise.State phaseBidding;
    [SerializeField] private AK.Wwise.State phaseCombat;

    [Header("Map States")]
    [SerializeField] private AK.Wwise.State mapNone;
    [SerializeField] private AK.Wwise.State mapCliff;
    [SerializeField] private AK.Wwise.State mapLava;

    [Header("Players States")]
    [SerializeField] private AK.Wwise.State playersTwo;
    [SerializeField] private AK.Wwise.State playersThree;
    [SerializeField] private AK.Wwise.State playersFour;

    private bool musicSystemIsPlaying;
    private PersistentGameStateManager.GameState currentGameState;
    private NetworkManager networkManager;

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientCountChanged;
            networkManager.OnClientDisconnectCallback += OnClientCountChanged;
        }

        SetMapForScene(SceneManager.GetActiveScene());

        var gameStateManager = GetComponent<PersistentGameStateManager>();
        SetGameState(gameStateManager != null
            ? gameStateManager.State
            : PersistentGameStateManager.GameState.Menu);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientCountChanged;
            networkManager.OnClientDisconnectCallback -= OnClientCountChanged;
        }
    }

    public void SetGameState(PersistentGameStateManager.GameState gameState)
    {
        currentGameState = gameState;

        // Clear the previous arena while loading the next round. Combat music
        // stays silent until the actual combat scene identifies its map.
        if (gameState != PersistentGameStateManager.GameState.Combat)
            SetState(mapNone, "Map/None");

        switch (gameState)
        {
            case PersistentGameStateManager.GameState.Menu:
                SetState(phaseMenu, "Game_Phase/Menu");
                break;
            case PersistentGameStateManager.GameState.Lobby:
                // MX_Lobby is a Players-driven Music Switch Track. Set its
                // State before selecting the Lobby phase so it has a sequence
                // ready on the first frame of the transition.
                SetLobbyPlayerCount(GetConnectedPlayerCount());
                SetState(phaseLobby, "Game_Phase/Lobby");
                break;
            case PersistentGameStateManager.GameState.Bidding:
            case PersistentGameStateManager.GameState.Shopping:
                // The shop is part of the auction loop, so it keeps the
                // bidding music instead of restarting another cue.
                SetState(phaseBidding, "Game_Phase/Bidding");
                break;
            case PersistentGameStateManager.GameState.Combat:
                SetState(phaseCombat, "Game_Phase/Combat");
                break;
        }

        StartMusic();
    }

    public void SetLobbyPlayerCount(int playerCount)
    {
        if (playerCount >= 4)
            SetState(playersFour, "Players/Four");
        else if (playerCount == 3)
            SetState(playersThree, "Players/Three");
        else
            SetState(playersTwo, "Players/Two");
    }

    public void StartMusic()
    {
        if (musicSystemIsPlaying)
            return;

        if (!IsAssigned(playMusicSystem, "Play_MX_System"))
            return;

        uint playingId = playMusicSystem.Post(gameObject);
        musicSystemIsPlaying = playingId != AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    public void StopMusic()
    {
        if (!musicSystemIsPlaying)
            return;

        if (IsAssigned(stopMusicSystem, "Stop_MX_System"))
            stopMusicSystem.Post(gameObject);

        musicSystemIsPlaying = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        SetMapForScene(scene);
    }

    private void OnClientCountChanged(ulong _)
    {
        if (currentGameState == PersistentGameStateManager.GameState.Lobby)
            SetLobbyPlayerCount(GetConnectedPlayerCount());
    }

    private int GetConnectedPlayerCount()
    {
        if (NetworkManager.Singleton == null)
            return 2;

        return Mathf.Clamp(NetworkManager.Singleton.ConnectedClientsList.Count, 2, 4);
    }

    private void SetMapForScene(Scene scene)
    {
        switch (scene.name)
        {
            case CliffSceneName:
                SetState(mapCliff, "Map/Cliff");
                break;
            case LavaSceneName:
                SetState(mapLava, "Map/Lava");
                break;
            default:
                SetState(mapNone, "Map/None");
                break;
        }
    }

    private void SetState(AK.Wwise.State state, string stateName)
    {
        if (!IsAssigned(state, stateName))
            return;

        state.SetValue();
    }

    private bool IsAssigned(AK.Wwise.BaseType wwiseObject, string objectName)
    {
        if (wwiseObject != null && wwiseObject.IsValid())
            return true;

        Debug.LogWarning($"[GameAudioController] Wwise object '{objectName}' is not assigned.", this);
        return false;
    }
}
