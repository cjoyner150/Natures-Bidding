using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityUtils;
using System;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine.Events;
using System.Threading.Tasks;
using Unity.Collections;
using System.Threading;

public class NetworkSessionManager : Singleton<NetworkSessionManager>
{
    ISession activeSession;

    ISession ActiveSession
    {
        get => activeSession;
        set
        {
            activeSession = value;
            Debug.Log($"New Active Session is {activeSession}");
        }
    }

    private bool _isBusy = false;

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();

            DontDestroyOnLoad(gameObject);
        }
    }

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(true);
            }
            AuthenticationService.Instance.ClearSessionToken();

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await AuthenticationService.Instance.UpdatePlayerNameAsync("Player");
            Debug.Log($"Player Initialized with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

    }

    private void OnNetworkSceneEvent(SceneEvent sceneEvent)
    {
        if (NetworkManager.Singleton.IsServer) return;
        if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId) return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Synchronize:
                PersistentGameStateManager.Instance.LoadingPanel.SetActive(true);
                break;
            case SceneEventType.Load:
                if (sceneEvent.AsyncOperation != null)
                    TrackClientLoadProgress(sceneEvent.AsyncOperation).Forget();
                break;
        }
    }

    private async UniTaskVoid TrackClientLoadProgress(AsyncOperation op)
    {
        while (!op.isDone)
        {
            PersistentGameStateManager.Instance.UpdateLoadingProgress(op.progress / 0.9f * 100f);
            await UniTask.Yield();
        }
    }

    #region Handle Session Events

    private void HookSessionEvents(ISession session)
    {
        session.Deleted += OnSessionDeleted;
        session.RemovedFromSession += OnRemovedFromSession;
        session.StateChanged += OnSessionStateChanged;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnNetworkSceneEvent;
    }

    private void UnhookSessionEvents(ISession session)
    {
        session.Deleted -= OnSessionDeleted;
        session.RemovedFromSession -= OnRemovedFromSession;
        session.StateChanged -= OnSessionStateChanged;
        NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
    }

    private void OnSessionDeleted()
    {
        Debug.Log("Session deleted by host.");
        HandleSessionEnded().Forget();
    }

    private void OnRemovedFromSession()
    {
        Debug.Log("Removed from session.");
        HandleSessionEnded().Forget();
    }

    private void OnSessionStateChanged(SessionState state)
    {
        Debug.Log($"Session state changed: {state}");
        if (state == SessionState.Disconnected)
            HandleSessionEnded().Forget();
    }

    private async UniTaskVoid HandleSessionEnded()
    {
        await SafeLeaveAsync();

        PersistentGameStateManager.Instance.ReturnToMenu();
    }

    #endregion

    #region Handle Player Data
    public async UniTask ChangePlayerName(string playerName)
    {
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        Debug.Log($"Player updated with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
    }

    #endregion

    #region Manage Session Join and Leave

    public async UniTask StartSessionAsHost()
    {
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback += (request, response) =>
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
        };

        var options = new SessionOptions
        {
            MaxPlayers = 4,
            IsPrivate = false,
            IsLocked = false,
        }.WithRelayNetwork();

        ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        Debug.Log($"Session started. Id: {ActiveSession.Id}, Code: {ActiveSession.Code}");

        StartLobbyHeartbeat();
        HookSessionEvents(ActiveSession);
    }

    async UniTaskVoid JoinSessionByID(string sessionId)
    {
        ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
        Debug.Log($"Session with id: {sessionId} joined!");
    }

    public async UniTask<bool> JoinSessionByCode(string sessionCode)
    {
        ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode);

        if (ActiveSession != null)
        {
            HookSessionEvents(ActiveSession);
            Debug.Log($"Session with id: {sessionCode} joined!");
            return true;
        }
        else return false;
    }

    public async UniTask QuickJoin()
    {
        // Prevent overlapping join/leave operations
        if (_isBusy)
        {
            Debug.LogWarning("QuickJoin called while session operation already in progress. Ignoring.");
            return;
        }

        _isBusy = true;

        try
        {
            // Fully leave any existing session before doing anything else
            await SafeLeaveAsync();

            await UniTask.Yield();
            await UniTask.Delay(1000);

            var sessions = (await QuerySessions()).ToList();

            if (sessions.Count > 0)
            {
                Debug.Log($"Found {sessions.Count} session(s). Joining {sessions[0].Id}...");
                ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessions[0].Id);
                HookSessionEvents(ActiveSession);
                Debug.Log($"Joined session. Code: {ActiveSession.Code}");
            }
            else
            {
                Debug.Log("No sessions found. Starting as host...");
                await StartSessionAsHost();
            }
        }
        catch (SessionException e)
        {
            Debug.LogError($"QuickJoin failed: {e.Message}");
            ActiveSession = null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ActiveSession = null;
        }
        finally
        {
            _isBusy = false;
        }
    }

    async UniTask SafeLeaveAsync()
    {
        StopLobbyHeartbeat();

        if (ActiveSession != null)
        {
            UnhookSessionEvents(ActiveSession);

            try
            {
                await ActiveSession.LeaveAsync();
            }
            catch (SessionException e) when (e.Message.Contains("connection was lost"))
            {
                Debug.LogWarning($"Session leave warning (non-fatal): {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SafeLeave warning (non-fatal): {e.Message}");
            }
            finally
            {
                ActiveSession = null;
            }
        }
    }

    async UniTaskVoid KickPlayer(string playerId)
    {
        if (activeSession == null || !activeSession.IsHost) return;
        await ActiveSession.AsHost().RemovePlayerAsync(playerId);
    }

    async UniTask<IList<ISessionInfo>> QuerySessions()
    {
        var options = new QuerySessionsOptions();

        var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
        return results.Sessions;
    }

    public async UniTask LeaveSession()
    {
        if (_isBusy)
        {
            Debug.LogWarning("LeaveSession called while busy. Ignoring.");
            return;
        }

        _isBusy = true;
        try
        {
            await SafeLeaveAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    #endregion

    #region Session Heartbeat

    private CancellationTokenSource _heartbeatCts;

    public void StartLobbyHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        HeartbeatLoopAsync(_heartbeatCts.Token).Forget();
    }

    public void StopLobbyHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts = null;
    }

    private async UniTaskVoid HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ActiveSession != null)
        {
            try
            {
                await ActiveSession.AsHost().RefreshAsync();
            }
            catch (SessionException e)
            {
                Debug.LogWarning($"Heartbeat warning (non-fatal): {e.Message}");
            }

            await UniTask.Delay(15000, cancellationToken: ct);
        }
    }

    #endregion

}
