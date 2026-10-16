using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Netcode;
using Steamworks;
using System;

public class MenuNetworkUI : MonoBehaviour
{
    private NetworkSessionManager sessionManager;

    private void Start()
    {
        sessionManager = NetworkSessionManager.Instance;
    }

    private bool Busy => (
        PersistentGameStateManager.Instance.IsLoading ||
        PersistentGameStateManager.Instance.IsReturningToMenu ||
        sessionManager.IsBusy
    );

    private static void ClearErrors()
    {
        if (MenuNetworkErrorHandler.HasInstance) 
            MenuNetworkErrorHandler.Instance.HideErrors();
    }

    public async void JoinSessionByButton(TMP_InputField input)
    {
        if (Busy) return;
        ClearErrors();

        PersistentGameStateManager.Instance.SetLoadingState("Validating Session...");

        try
        {
            if (!await sessionManager.JoinSessionByCode(input.text))
                await FailAsync($"Couldn't find a session with code {input.text}.");
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Warning, "Join by code failed.", e);
            await FailAsync($"Couldn't join session {input.text}. Check the code and try again.");
        }
    }

    public async void QuickJoinByButton()
    {
        if (Busy) return;
        ClearErrors();

        PersistentGameStateManager.Instance.SetLoadingState("Looking for sessions...");

        try 
        { 
            await sessionManager.QuickJoin(); 
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Error, "QuickJoin escaped its own handling.", e);
            await FailAsync("Couldn't find or create a game. Please try again.");
        }
    }

    public async void StartSessionAsHostByButton()
    {
        if (Busy) return;
        ClearErrors();

        PersistentGameStateManager.Instance.SetLoadingState("Hosting session...");

        try 
        { 
            await sessionManager.StartSessionAsHost(); 
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Error, "Host failed.", e);
            await FailAsync("Couldn't host a session. Please try again.");
        }
    }

    public async void EnterNameByButton(TMP_InputField input)
    {
        try 
        { 
            await sessionManager.ChangePlayerName(input.text); 
        }
        catch (Exception e) 
        { 
            GameLogger.LogException(LogSeverity.Warning, "Name change failed.", e); 
        }
    }

    public async void QuitGameByButton()
    {
        if (PersistentGameStateManager.Instance.IsLoading) return;
        PersistentGameStateManager.Instance.SetLoadingState("Ending session...");

        try
        {
            await sessionManager.LeaveSession();

            if (SteamClient.IsValid) 
                await PersistentSteamManager.Instance.ShutdownSteam();
        }
        catch (Exception e) 
        { 
            GameLogger.LogException(LogSeverity.Warning, "Shutdown error.", e); 
        
        }
        finally 
        { 
            Application.Quit(); 
        }
    }

    // Cleans up any half-started session, then shows the error.
    private async UniTask FailAsync(string playerMessage)
    {
        try
        {
            await sessionManager.LeaveSession();
        }
        catch { } // Logged inside method

        PersistentGameStateManager.Instance.ClearLoadingState();
        MenuNetworkErrorHandler.ThrowMenuErrorToQueue(playerMessage);
    }
}

public enum LeaveReason
{
    UserRequested,      // no message
    RunComplete,        // optional "match over" message
    HostEndedSession,   // OnSessionDeleted
    RemovedFromSession, // OnRemovedFromSession (kicked)
    ConnectionLost,     // OnDisconnectedFromHost
    TransportFailure,   // OnTransportFailure
    JoinFailed,         // QuickJoin / JoinSessionByCode failures
    SessionInvalid,     // heartbeat saw Deleted/Disconnected
    InternalError
}

public static class LeaveReasonText
{
    public static string Describe(LeaveReason reason, string detail = null) => reason switch
    {
        LeaveReason.HostEndedSession => "The host ended the session.",
        LeaveReason.RemovedFromSession => "You were removed from the session.",
        LeaveReason.ConnectionLost => string.IsNullOrEmpty(detail) ? "Lost connection to the host." : detail,
        LeaveReason.TransportFailure => "Network connection failed.",
        LeaveReason.JoinFailed => "Couldn't join a game. Please try again.",
        LeaveReason.SessionInvalid => "The session is no longer available.",
        LeaveReason.InternalError => "Something went wrong. You've been returned to the menu.",
        _ => null   // UserRequested / RunComplete: no error
    };
}
