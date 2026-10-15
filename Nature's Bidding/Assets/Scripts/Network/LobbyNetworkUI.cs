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
using System;

public class LobbyNetworkUI : MonoBehaviour
{
    [Header("Ready Config")]
    [SerializeField] GameObject readyPanel;
    [SerializeField] MMRotationShaker readyPanelShaker;
    [SerializeField] Image playerMnkReadyVisual;
    [SerializeField] Image playerControllerReadyVisual;
    [SerializeField] Image playerConfirmedReadyVisual;
    [SerializeField] TextMeshProUGUI readyTMP;

    [Header("Lobby Info Config")]
    [SerializeField] TextMeshProUGUI lobbyCodeTMP;
    [SerializeField] Image showCodeEyeVisual;
    [SerializeField] Image hideCodeEyeVisual;
    [SerializeField] Image toggleCodeMnkVisual;
    [SerializeField] Image toggleCodeControllerVisual;
    private bool codeHidden = true;


    public static Action OnPlayerReadyEvent;
    public static Action OnPlayerToggleSecretsEvent;

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
        UpdateHideCodeState(codeHidden);

        UpdateInputPrompts();
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
                readyTMP.text = "Waiting...";
                playerConfirmedReadyVisual.enabled = false;
                playerMnkReadyVisual.enabled = false;
                playerControllerReadyVisual.enabled = false;
                canReady = false;
                break;
            case ReadyPanelState.WaitingForReady:
                readyTMP.text = "Ready?";
                canReady = true; 
                playerConfirmedReadyVisual.enabled = false;
                playerMnkReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard;
                playerControllerReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
                break;
            case ReadyPanelState.HasReadied:
                readyTMP.text = "Ready!";
                playerConfirmedReadyVisual.enabled = true;
                playerMnkReadyVisual.enabled = false;
                playerControllerReadyVisual.enabled = false;
                canReady = false;
                break;
        }
        
    }

    private void UpdateHideCodeState(bool hideCode)
    {
        codeHidden = hideCode;

        if (hideCode)
        {
            showCodeEyeVisual.enabled = false;
            hideCodeEyeVisual.enabled = true;
            lobbyCodeTMP.text = "#####";
        }
        else
        {
            showCodeEyeVisual.enabled = true;
            hideCodeEyeVisual.enabled = false;
            lobbyCodeTMP.text = NetworkSessionManager.Instance?.ActiveSession?.Code;
        }

    }

    private void OnEnable()
    {

        OnPlayerReadyEvent += OnPlayerReady;
        OnPlayerToggleSecretsEvent += OnPlayerToggleSecrets;

        InputDeviceTracker.OnInputTypeChanged += OnInputTypeChanged;

        LobbyServerHandler.OnEnoughPlayersRegistered.AddListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.AddListener(OnPlayerRequirementDropped);
    }

    private void OnDisable()
    {

        OnPlayerReadyEvent -= OnPlayerReady;
        OnPlayerToggleSecretsEvent -= OnPlayerToggleSecrets;

        InputDeviceTracker.OnInputTypeChanged -= OnInputTypeChanged;

        LobbyServerHandler.OnEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementDropped);
    }

    void OnPlayerReady()
    {
        if (readied || !canReady) return;

        LobbyServerHandler.Instance.PlayerReadiedServerRpc();
        readied = true;

        UpdateReadyVisualState(ReadyPanelState.HasReadied);
    }

    void OnPlayerToggleSecrets()
    {
        UpdateHideCodeState(!codeHidden);
    }

    void OnPlayerRequirementMet()
    {
        if (canReady || readied) return; // Client already knows the player requirement is met and should not have text reset

        UpdateReadyVisualState(ReadyPanelState.WaitingForReady);
    }

    void OnPlayerRequirementDropped()
    {
        UpdateReadyVisualState(ReadyPanelState.WaitingOnPlayers);
        readied = false;
    }

    void OnInputTypeChanged(InputDeviceTracker.InputType _) => UpdateInputPrompts();

    private void UpdateInputPrompts()
    {
        if (currentReadyState == ReadyPanelState.WaitingForReady)
        {
            UpdateReadyInputPrompt();
        }

        UpdateToggleSecretsInputPrompt();
    }

    private void UpdateToggleSecretsInputPrompt()
    {
        toggleCodeMnkVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard;
        toggleCodeControllerVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
    }

    private void UpdateReadyInputPrompt()
    {
        playerMnkReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard;
        playerControllerReadyVisual.enabled = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
    }
}
