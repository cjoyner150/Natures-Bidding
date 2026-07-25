using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Steamworks;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;
using MoreMountains.Feedbacks;
using Unity.VisualScripting;

public class LobbyNetworkUI : MonoBehaviour
{
    [SerializeField] GameObject readyPanel;
    [SerializeField] MMRotationShaker readyPanelShaker;
    [SerializeField] Image playerMnkReadyVisual;
    [SerializeField] Image playerControllerReadyVisual;
    [SerializeField] Image playerConfirmedReadyVisual;
    [SerializeField] TextMeshProUGUI readyTMP;
    [SerializeField] InputAction playerReadyAction;
    bool readied = false;
    bool canReady = false;

    private NetworkSessionManager sessionManager;

    private enum ReadyPanelState
    {
        WaitingOnPlayers,
        WaitingForReady,
        HasReadied
    }

    private ReadyPanelState currentReadyState = ReadyPanelState.WaitingOnPlayers;

    private void Start()
    {
        sessionManager = NetworkSessionManager.Instance;
        UpdateReadyVisualState(ReadyPanelState.WaitingOnPlayers);
    }

    private void UpdateReadyVisualState(ReadyPanelState newState)
    {
        if (readyPanelShaker.IsDestroyed() || readyPanelShaker == null) return; // scene is switching

        var prevState = currentReadyState;
        currentReadyState = newState;

        readyPanelShaker.enabled = true;

        switch (newState)
        {
            case ReadyPanelState.WaitingOnPlayers:
                readyTMP.text = "Waiting on more players...";
                playerConfirmedReadyVisual.enabled = false;
                playerMnkReadyVisual.enabled = false;
                playerControllerReadyVisual.enabled = false;
                break;
            case ReadyPanelState.WaitingForReady:
                readyTMP.text = "Ready?";
                playerConfirmedReadyVisual.enabled = false;
                playerMnkReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard;
                playerControllerReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
                break;
            case ReadyPanelState.HasReadied:
                readyTMP.text = "Ready!";
                playerConfirmedReadyVisual.enabled = true;
                playerMnkReadyVisual.enabled = false;
                playerControllerReadyVisual.enabled = false;
                break;
        }
        
    }

    private void OnEnable()
    {
        playerReadyAction.Enable();
        playerReadyAction.performed += OnPlayerReady;

        InputDeviceTracker.OnInputTypeChanged += OnInputTypeChanged;

        LobbyServerHandler.OnEnoughPlayersRegistered.AddListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.AddListener(OnPlayerRequirementDropped);
        CombatServerHandler.OnCombatBegin.AddListener(OnCombatBegin);
    }

    private void OnDisable()
    {
        playerReadyAction.Disable();
        playerReadyAction.performed -= OnPlayerReady;

        InputDeviceTracker.OnInputTypeChanged -= OnInputTypeChanged;

        LobbyServerHandler.OnEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementDropped);
        CombatServerHandler.OnCombatBegin.RemoveListener(OnCombatBegin);
    }

    void OnPlayerReady(InputAction.CallbackContext ctx)
    {
        if (readied || !canReady) return;

        LobbyServerHandler.Instance.PlayerReadiedServerRpc();
        readied = true;

        UpdateReadyVisualState(ReadyPanelState.HasReadied);
    }

    void OnPlayerRequirementMet()
    {
        if (canReady || readied) return; // Client already knows the player requirement is met and should not have text reset

        UpdateReadyVisualState(ReadyPanelState.WaitingForReady);
        canReady = true;
    }

    void OnPlayerRequirementDropped()
    {
        UpdateReadyVisualState(ReadyPanelState.WaitingOnPlayers);
        readied = false;
        canReady = false;
    }

    void OnInputTypeChanged(InputDeviceTracker.InputType inputType)
    {
        if (currentReadyState == ReadyPanelState.WaitingForReady)
        {
            playerMnkReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard;
            playerControllerReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
        }
    }

    void OnCombatBegin()
    {
        
    }

    public void LeaveSessionByButton()
    {
        LeaveSession();
    }

    public async void QuitGameByButton()
    {
        await sessionManager.LeaveSession();

        if (SteamClient.IsValid)
        {
            await PersistentSteamManager.Instance.ShutdownSteam();
        }

        Application.Quit();
    }

    public void LeaveSession()
    {
        PlayerPauseManager.Instance.ForceResume();

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        PersistentGameStateManager.Instance.ReturnToMenu().Forget();
    }
}
