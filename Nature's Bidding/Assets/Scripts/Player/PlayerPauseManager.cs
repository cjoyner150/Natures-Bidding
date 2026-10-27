using Cysharp.Threading.Tasks;
using Steamworks;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityUtils;

public class PlayerPauseManager : Singleton<PlayerPauseManager>
{
    // Combat levels use the 3D pause menu, every other scene uses the 2D one.
    private const string VolcanoCombatSceneName = "LavaGameplay";
    private const string CliffsCombatSceneName = "CliffGameplay";

    [Serializable]
    private class PauseMenuVariant
    {
        public GameObject root;
        public GameObject hostPanel;
        public GameObject lobbyWaitingPanel;

        public void SetActive(bool active, bool isHost)
        {
            root.SetActive(active);
            if (lobbyWaitingPanel != null) lobbyWaitingPanel.SetActive(active);
            if (hostPanel != null) hostPanel.SetActive(active && isHost);
        }
    }

    [SerializeField] private PauseMenuVariant pauseMenu3D;
    [SerializeField] private PauseMenuVariant pauseMenu2D;

    private PauseMenuVariant activeVariant;

    public static Action OnPausePressed;
    public static Action OnResumed;
    public static Action OnPaused;
    [HideInInspector] public bool Paused { get; private set; }

    private bool pauseEnabled = true;

    protected override void Awake()
    {
        if (HasInstance)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();
        DontDestroyOnLoad(gameObject);

        Paused = false;
        SetActiveVariant(IsCombatScene(SceneManager.GetActiveScene().name));
    }

    private void OnEnable()
    {
        OnPausePressed += OnPauseEvent;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        OnPausePressed -= OnPauseEvent;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static bool IsCombatScene(string sceneName)
    {
        return sceneName == VolcanoCombatSceneName || sceneName == CliffsCombatSceneName;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Pause state doesn't carry meaningfully across a scene change, so clear it instead of risking stuck UI.
        ForceResume();
        SetActiveVariant(IsCombatScene(scene.name));
    }

    private void SetActiveVariant(bool isCombatScene)
    {
        var newVariant = isCombatScene ? pauseMenu3D : pauseMenu2D;
        if (activeVariant != null && activeVariant != newVariant)
        {
            activeVariant.root.SetActive(false);
        }
        activeVariant = newVariant;
        activeVariant.root.SetActive(false);
    }

    public void SetPauseAbility(bool isEnabled)
    {
        pauseEnabled = isEnabled;
        if (!pauseEnabled)
        {
            ForceResume();
        }
    }

    void OnPauseEvent()
    {
        if (!pauseEnabled) return;

        if (Paused)
        {
            UnpauseGame();
        }
        else
        {
            PauseGame();
        }
    }
    void PauseGame()
    {
        Paused = true;

        activeVariant.SetActive(true, NetworkManager.Singleton.IsHost);

        OnPaused?.Invoke();
    }

    void UnpauseGame()
    {
        Paused = false;

        activeVariant.SetActive(false, NetworkManager.Singleton.IsHost);

        OnResumed?.Invoke();
    }

    public void ForceResume()
    {
        if (Paused)
        {
            UnpauseGame();
        }
    }

    public void OnResumeButton()
    {
        UnpauseGame();
    }

    public void LeaveSessionByButton()
    {
        LeaveSession();
    }

    public async void QuitGameByButton()
    {
        await NetworkSessionManager.Instance.LeaveSession();

        if (SteamClient.IsValid)
        {
            await PersistentSteamManager.Instance.ShutdownSteam();
        }

        Application.Quit();
    }

    public void LeaveSession()
    {
        ForceResume();

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        PersistentGameStateManager.Instance.ReturnToMenu().Forget();
    }

}
