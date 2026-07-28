using Cysharp.Threading.Tasks;
using Steamworks;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public class PlayerPauseManager : Singleton<PlayerPauseManager>
{
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject lobbyWaitingPanel;
    [SerializeField] GameObject hostPanel;

    public static Action OnPausePressed;
    public static Action OnResumed;
    public static Action OnPaused;
    [HideInInspector] public bool Paused { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Paused = false;
        pausePanel.SetActive(false);
        hostPanel.SetActive(false);
    }

    private void OnEnable()
    {
        OnPausePressed += OnPauseEvent;
    }

    private void OnDisable()
    {
        OnPausePressed -= OnPauseEvent;
    }

    void OnPauseEvent()
    {
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

        pausePanel.SetActive(true);
        if (NetworkManager.Singleton.IsHost)
        {
            hostPanel.SetActive(true);
        }

        if (lobbyWaitingPanel != null) lobbyWaitingPanel.SetActive(false);

        OnPaused?.Invoke();
    }

    void UnpauseGame()
    {
        Paused = false;

        pausePanel.SetActive(false);
        hostPanel.SetActive(false);
        
        if (lobbyWaitingPanel != null) lobbyWaitingPanel.SetActive(true);

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
