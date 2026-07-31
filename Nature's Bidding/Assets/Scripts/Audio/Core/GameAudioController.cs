using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AkGameObj))]
public sealed class GameAudioController : MonoBehaviour
{
    private const string CliffSceneName = "CliffGameplay";
    private const string LavaSceneName = "LavaGameplay";
    private const float CombatPlayerStatePollInterval = 0.2f;

    [Header("Music Events")]
    [SerializeField] private AK.Wwise.Event playMusicSystem;
    [SerializeField] private AK.Wwise.Event stopMusicSystem;

    [Header("Ambience Events")]
    [SerializeField] private AK.Wwise.Event playForestAmbience;
    [SerializeField] private AK.Wwise.Event stopForestAmbience;

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
    private bool forestAmbienceIsPlaying;
    private PersistentGameStateManager.GameState currentGameState;
    private NetworkManager networkManager;
    private int currentPlayersStateCount = -1;
    private float nextCombatPlayerStatePollTime;

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

    private void Update()
    {
        if (currentGameState != PersistentGameStateManager.GameState.Combat)
            return;

        if (!IsCombatScene(SceneManager.GetActiveScene()))
            return;

        if (Time.unscaledTime < nextCombatPlayerStatePollTime)
            return;

        nextCombatPlayerStatePollTime = Time.unscaledTime + CombatPlayerStatePollInterval;
        UpdateCombatPlayersState();
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
                SetPlayerCountState(GetConnectedPlayerCount());
                SetState(phaseLobby, "Game_Phase/Lobby");
                break;
            case PersistentGameStateManager.GameState.Bidding:
            case PersistentGameStateManager.GameState.Shopping:
                // The shop is part of the auction loop, so it keeps the
                // bidding music instead of restarting another cue.
                SetState(phaseBidding, "Game_Phase/Bidding");
                break;
            case PersistentGameStateManager.GameState.Combat:
                // Both combat maps use the same Players State Group. Seed it
                // before entering the Combat phase, then Update() follows the
                // replicated PlayerHealth values as combatants are eliminated.
                SetPlayerCountState(GetConnectedPlayerCount());
                nextCombatPlayerStatePollTime = 0f;
                SetState(phaseCombat, "Game_Phase/Combat");
                break;
        }

        StartMusic();
        RefreshForestAmbience(SceneManager.GetActiveScene());
    }

    public void SetLobbyPlayerCount(int playerCount)
    {
        SetPlayerCountState(playerCount);
    }

    public void SetPlayerCountState(int playerCount)
    {
        int playersStateCount = Mathf.Clamp(playerCount, 2, 4);
        if (playersStateCount == currentPlayersStateCount)
            return;

        if (playersStateCount >= 4)
            SetState(playersFour, "Players/Four");
        else if (playersStateCount == 3)
            SetState(playersThree, "Players/Three");
        else
            SetState(playersTwo, "Players/Two");

        currentPlayersStateCount = playersStateCount;
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
        RefreshForestAmbience(scene);

        if (IsCombatScene(scene))
            nextCombatPlayerStatePollTime = 0f;
    }

    private void OnClientCountChanged(ulong _)
    {
        if (currentGameState == PersistentGameStateManager.GameState.Lobby)
            SetPlayerCountState(GetConnectedPlayerCount());
    }

    private int GetConnectedPlayerCount()
    {
        if (NetworkManager.Singleton == null)
            return 2;

        return Mathf.Clamp(NetworkManager.Singleton.ConnectedClientsList.Count, 2, 4);
    }

    private void UpdateCombatPlayersState()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int spawnedPlayers = 0;
        int initializedPlayers = 0;
        int alivePlayers = 0;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned)
                continue;

            spawnedPlayers++;

            // Health begins at zero while each player's replicated stats are
            // initialized. Keep the seeded connection count until the whole
            // local combat roster is ready.
            if (player.maxHealth.Value <= 0f)
                continue;

            initializedPlayers++;

            if (player.health.Value > 0f)
                alivePlayers++;
        }

        if (spawnedPlayers == 0 || initializedPlayers < spawnedPlayers || alivePlayers == 0)
            return;

        // Wwise currently exposes Two, Three, and Four. One survivor therefore
        // remains on Players/Two through the victory sequence.
        SetPlayerCountState(alivePlayers);
    }

    private static bool IsCombatScene(Scene scene)
    {
        return scene.name == CliffSceneName || scene.name == LavaSceneName;
    }

    private void RefreshForestAmbience(Scene scene)
    {
        bool shouldPlay =
            currentGameState == PersistentGameStateManager.GameState.Lobby ||
            (currentGameState == PersistentGameStateManager.GameState.Combat &&
             scene.name == CliffSceneName);

        if (shouldPlay)
            StartForestAmbience();
        else
            StopForestAmbience();
    }

    private void StartForestAmbience()
    {
        if (forestAmbienceIsPlaying)
            return;

        if (!IsAssigned(playForestAmbience, "Play_AMB_Forest"))
            return;

        uint playingId = playForestAmbience.Post(gameObject);
        forestAmbienceIsPlaying = playingId != AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void StopForestAmbience()
    {
        if (!forestAmbienceIsPlaying)
            return;

        if (IsAssigned(stopForestAmbience, "Stop_AMB_Forest"))
            stopForestAmbience.Post(gameObject);

        forestAmbienceIsPlaying = false;
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
