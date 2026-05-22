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
using Steamworks;
using Steamworks.Data;

public class NetworkSessionManager : Singleton<NetworkSessionManager>
{
    ISession activeSession;

    public ISession ActiveSession
    {
        get => activeSession;
        set
        {
            activeSession = value;
            Debug.Log($"New Active Session is {activeSession}");
        }
    }

    public static Action OnSessionHosted;

    public bool HasActiveSession => ActiveSession != null;

    public bool IsBusy => _isBusy;
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

    public async UniTask WaitForAuth()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (AuthenticationService.Instance.IsSignedIn)
                AuthenticationService.Instance.SignOut(true);

            AuthenticationService.Instance.ClearSessionToken();
#if UNITY_EDITOR
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await AuthenticationService.Instance.UpdatePlayerNameAsync("EditorPlayer");
#else
            string identity = "unityauthentication";

            var ticket = await SteamUser.GetAuthTicketForWebApiAsync(identity);

            if (ticket == null)
            {
                Debug.LogError("Failed to get Steam auth ticket. Make sure this account has access to the game in Steamworks.");

                Application.Quit();
                return;
            }

            string ticketHex = BitConverter.ToString(ticket.Data)
                .Replace("-", "")
                .ToLower();

            await AuthenticationService.Instance.SignInWithSteamAsync(ticketHex, identity);

            string steamName = SteamClient.Name;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(steamName);

            Debug.Log($"Steam: Signed in as {steamName}");

            ticket.Cancel();
#endif
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
            PersistentGameStateManager.Instance.SetLoadingProgress(op.progress / 0.9f * 100f);
            await UniTask.Yield();
        }
    }

    #region Handle Session Events

    private void OnSessionConnected(ISession session)
    {
        if (NetworkManager.Singleton?.SceneManager == null)
            throw new InvalidOperationException("NetworkManager not ready during HookSessionEvents.");

        session.Deleted += OnSessionDeleted;
        session.RemovedFromSession += OnRemovedFromSession;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnNetworkSceneEvent;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnectedFromHost;

        if (session.IsHost) { 
            StartLobbyHeartbeat();
            OnSessionHosted?.Invoke();
        }
    }

    private void OnSessionDisconnected(ISession session)
    {
        session.Deleted -= OnSessionDeleted;
        session.RemovedFromSession -= OnRemovedFromSession;

        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromHost;
        }

        StopLobbyHeartbeat();
    }

    private void OnSessionDeleted()
    {
        Debug.Log("Session deleted by host.");
        PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnRemovedFromSession()
    {
        Debug.Log("Removed from session.");
        PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnDisconnectedFromHost(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;
        if (_isBusy) return;
        Debug.Log("Disconnected from host.");
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

    public async UniTask StartSessionAsHost(int maxRetries = 3)
    {
        PersistentGameStateManager.Instance.SetLoadingState("Creating session...");

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = (request, response) =>
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

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                Debug.Log($"Session started. Id: {ActiveSession.Id}, Code: {ActiveSession.Code}");
                OnSessionConnected(ActiveSession);
                return;
            }
            catch (SessionException e) when (e.Message.Contains("fetch relay join code") || e.Message.Contains("timeout"))
            {
                if (i < maxRetries - 1)
                {
                    Debug.LogWarning($"Failed to create session, retrying... (attempt {i + 1}/{maxRetries})");
                    await UniTask.Delay(1000);
                }
                else throw;
            }
        }
    }

    //async UniTaskVoid JoinSessionByID(string sessionId)
    //{
    //    ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
    //    Debug.Log($"Session with id: {sessionId} joined!");
    //}

    public async UniTask<bool> JoinSessionByCode(string sessionCode)
    {
        PersistentGameStateManager.Instance.SetLoadingState("Joining session...");

        ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode);

        if (ActiveSession != null)
        {
            OnSessionConnected(ActiveSession);
            Debug.Log($"Session with id: {sessionCode} joined!");
            return true;
        }
        else return false;
    }

    public async UniTask QuickJoin(int retryCount = 0)
    {
        const int maxRetries = 3;

        await UniTask.WaitUntil(() => !_isBusy && !PersistentGameStateManager.Instance.IsReturningToMenu);
        if (HasActiveSession)
        {
            Debug.LogWarning("QuickJoin called with an active session. Leave the session before joining a new one.");
            return;
        }
        _isBusy = true;
        bool shouldReturnToMenu = false;
        try
        {
            var sessions = (await QuerySessions()).ToList();
            if (sessions.Count > 0)
            {
                Debug.Log($"Found {sessions.Count} session(s). Joining {sessions[0].Id}...");
                try
                {
                    ActiveSession = await JoinSessionWithRetry(sessions[0].Id, 5);
                    OnSessionConnected(ActiveSession);
                    Debug.Log($"Joined session. Code: {ActiveSession.Code}");
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogWarning($"HookSessionEvents failed: {e.Message}");
                    await SafeLeaveAsync();
                    shouldReturnToMenu = true;
                }
                catch (SessionException e) when (e.Message.Contains("lobby not found") || e.Message.Contains("not found"))
                {
                    Debug.LogWarning($"Session gone, starting as host. ({e.Message})");
                    await StartSessionAsHost();
                }
            }
            else
            {
                Debug.Log("No sessions found. Starting as host...");
                await StartSessionAsHost();
            }
        }
        catch (Exception e) when (e.Message.Contains("Unexpected exception processing network metadata"))
        {
            if (retryCount < maxRetries)
            {
                Debug.LogWarning($"Network metadata exception — retrying QuickJoin ({retryCount + 1}/{maxRetries}).");
                PersistentGameStateManager.Instance.SetLoadingState($"Retrying... ({retryCount + 1}/{maxRetries})");
                _isBusy = false;
                await UniTask.Delay(2000);
                await QuickJoin(retryCount + 1);
                return;
            }
            Debug.LogError($"QuickJoin failed after {maxRetries} retries.");
            PersistentGameStateManager.Instance.SetLoadingState("Failed to connect. Returning to menu...");
            await UniTask.Delay(2000);
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (SessionException e)
        {
            Debug.LogError($"QuickJoin failed: {e.Message}");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        finally
        {
            _isBusy = false;
        }
        if (shouldReturnToMenu)
            PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private async UniTask<ISession> JoinSessionWithRetry(string sessionId, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
            }
            catch (Exception e) when (e.Message.Contains("Unexpected exception processing network metadata"))
            {
                if (i < maxRetries - 1)
                {
                    Debug.LogWarning($"Service still cleaning up, retrying in 1s... (attempt {i + 1}/{maxRetries})");
                    await UniTask.Delay(1000);
                }
                else throw;
            }
        }
        throw new Exception("Failed to join session after max retries.");
    }

    async UniTask SafeLeaveAsync()
    {

        if (ActiveSession != null)
        {
            OnSessionDisconnected(ActiveSession);

            try
            {
                await ActiveSession.LeaveAsync();
            }
            catch (SessionException e) when (e.Message.Contains("connection was lost"))
            {
                Debug.LogWarning($"Exception type: {e.GetType().FullName}, Session leave warning (non-fatal): {e.Message}");
            }
            catch (Exception e)
            {
                Debug.Log($"Exception type: {e.GetType().FullName}, Message: {e.Message}");
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
        var options = new QuerySessionsOptions
        {
            Count = 20,
            FilterOptions = new List<FilterOption>
            {
                new FilterOption(
                    FilterField.AvailableSlots, "0", FilterOperation.Greater
                ),
                new FilterOption(
                    FilterField.IsLocked, "false", FilterOperation.Equal
                )
            }
        };

        var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
        return results?.Sessions ?? new List<ISessionInfo>();
    }

    public async UniTask SetSessionLocked(bool locked)
    {
        if (ActiveSession == null || !ActiveSession.IsHost) return;

        try
        {
            var hostSession = ActiveSession.AsHost();
            hostSession.IsLocked = locked;
            await hostSession.SavePropertiesAsync();
        }
        catch (SessionException e)
        {
            Debug.LogWarning($"Failed to set session lock: {e.Message}");
        }
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
        int tickCount = 0;

        while (!ct.IsCancellationRequested && ActiveSession != null)
        {
            await UniTask.Delay(5000, cancellationToken: ct);
            tickCount++;

            try
            {
                if (ActiveSession.State == SessionState.Deleted ||
                    ActiveSession.State == SessionState.Disconnected)
                {
                    Debug.LogWarning("Session state invalid.");
                    PersistentGameStateManager.Instance.ReturnToMenu();
                    break;
                }

                if (tickCount % 3 == 0)
                {
                    await ActiveSession.AsHost().RefreshAsync();
                    Debug.Log($"Session state after refresh: {ActiveSession.State}");
                }
            }
            catch (SessionException e)
            {
                Debug.LogWarning($"Heartbeat warning (non-fatal): {e.Message}");
            }
        }
    }

    #endregion

}
