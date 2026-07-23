using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityUtils;

public class PlayerPauseManager : Singleton<PlayerPauseManager>
{
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject lobbyWaitingPanel;

    public Action OnPausePressed;
    public Action OnResumed;
    public Action OnPaused;
    [HideInInspector] public bool Paused { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Paused = false;
        pausePanel.SetActive(false);
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

        if (lobbyWaitingPanel != null) lobbyWaitingPanel.SetActive(false);

        OnPaused?.Invoke();
    }

    void UnpauseGame()
    {
        Paused = false;

        pausePanel.SetActive(false);

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

}
