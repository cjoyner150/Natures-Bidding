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
using Debug = UnityEngine.Debug;

public class NetworkSessionManager : Singleton<NetworkSessionManager>
{
    ISession activeSession;

    public ISession ActiveSession
    {
        get => activeSession;
        set
        {
            activeSession = value;
            GameLogger.Log(LogSeverity.Info, $"New Active Session is {activeSession?.Name}");
        }
    }

    public static Action OnSessionHosted;

    public bool HasActiveSession => ActiveSession != null;

    public bool IsBusy => _isBusy;
    private bool _isBusy = false;

    private DateTime _lastLeaveTime = DateTime.MinValue;
    private const int MinTimeSinceLeaveMs = 2000;

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            GameLogger.Log(LogSeverity.Info, $"=== Session started {DateTime.Now} ===");
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
                GameLogger.Log(LogSeverity.Error, "Failed to get Steam auth ticket. Make sure this account has access to the game in Steamworks.");

                Application.Quit();
                return;
            }

            string ticketHex = BitConverter.ToString(ticket.Data)
                .Replace("-", "")
                .ToLower();

            await AuthenticationService.Instance.SignInWithSteamAsync(ticketHex, identity);

            string steamName = SteamClient.Name;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(steamName);

            GameLogger.Log(LogSeverity.Info, $"Steam: Signed in as {steamName}");

            ticket.Cancel();
#endif
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Error, "Failed to Authenticate for Unity or Steam Services.", e);
        }
    }

    private void OnNetworkSceneEvent(SceneEvent sceneEvent)
    {
        if (NetworkManager.Singleton.IsServer) return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Synchronize:
                PersistentGameStateManager.Instance.SetLoadingState("Loading scene...", true);
                break;
            case SceneEventType.Load:
                if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                    return;
                if (sceneEvent.AsyncOperation != null)
                    TrackClientLoadProgress(sceneEvent.AsyncOperation).Forget();
                break;
            case SceneEventType.LoadComplete:
            case SceneEventType.SynchronizeComplete:
                if (sceneEvent.ClientId != NetworkManager.Singleton.LocalClientId)
                    return;
                PersistentGameStateManager.Instance.ClearLoadingState();
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
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;

        if (session.IsHost)
        {
            StartLobbyHeartbeat();
            OnSessionHosted?.Invoke();
        }
    }

    private void OnSessionDisconnected(ISession session)
    {
        PersistentPlayerRegistry.Instance.ApplyClear();

        session.Deleted -= OnSessionDeleted;
        session.RemovedFromSession -= OnRemovedFromSession;

        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromHost;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;

        StopLobbyHeartbeat();
    }

    private void OnTransportFailure()
    {
        GameLogger.Log(LogSeverity.Error, "Unity Transport Failed.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnSessionDeleted()
    {
        GameLogger.Log(LogSeverity.Info, "Session deleted by host.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnRemovedFromSession()
    {
        GameLogger.Log(LogSeverity.Info, "Removed from session.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnDisconnectedFromHost(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;
        if (_isBusy) return;
        GameLogger.Log(LogSeverity.Info, "Disconnected from host.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    #endregion

    #region Handle Player Data
    public async UniTask ChangePlayerName(string playerName)
    {
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        GameLogger.Log(LogSeverity.Info, $"Player updated with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
    }

    #endregion

    #region Manage Session Join and Leave

    public async UniTask StartSessionAsHost(int maxRetries = 3)
    {
        GameLogger.Log(LogSeverity.Debug, "[StartSessionAsHost] START");
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
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { "version", new SessionProperty(Application.version, VisibilityPropertyOptions.Public, PropertyIndex.String1) }
            }
        }.WithRelayNetwork();

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                GameLogger.Log(LogSeverity.Info, $"[StartSessionAsHost] Session created. Id: {ActiveSession.Id}, Code: {ActiveSession.Code}");
                OnSessionConnected(ActiveSession);
                GameLogger.Log(LogSeverity.Debug, "[StartSessionAsHost] OnSessionConnected complete, returning");
                return;
            }
            catch (SessionException e) when (e.Message.Contains("fetch relay join code") || e.Message.Contains("timeout"))
            {
                GameLogger.Log(LogSeverity.Warning, $"[StartSessionAsHost] Retry-worthy exception: {e.Message}");
                if (i < maxRetries - 1)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[StartSessionAsHost] retrying attempt {i + 1}/{maxRetries}");
                    await UniTask.Delay(1000);
                }
                else throw;
            }
        }
    }

    //async UniTaskVoid JoinSessionByID(string sessionId)
    //{
    //    ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
    //    GameLogger.Log(LogSeverity.Info, $"Session with id: {sessionId} joined!");
    //}

    public async UniTask<bool> JoinSessionByCode(string sessionCode)
    {
        PersistentGameStateManager.Instance.SetLoadingState("Joining session...");

        ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode);

        if (ActiveSession != null)
        {
            OnSessionConnected(ActiveSession);
            GameLogger.Log(LogSeverity.Info, $"Session with id: {sessionCode} joined!");
            return true;
        }
        else return false;
    }

    public async UniTask QuickJoin(int retryCount = 0)
    {
        const int maxRetries = 3;

        GameLogger.Log(LogSeverity.Debug, $"[QuickJoin] START retryCount={retryCount}, _isBusy={_isBusy}");
        await UniTask.WaitUntil(() => !_isBusy && !PersistentGameStateManager.Instance.IsReturningToMenu);
        GameLogger.Log(LogSeverity.Debug, "[QuickJoin] passed busy/returning wait");

        var timeSinceLeave = (DateTime.UtcNow - _lastLeaveTime).TotalMilliseconds;
        GameLogger.Log(LogSeverity.Debug, $"[QuickJoin] timeSinceLeave={timeSinceLeave}ms");
        if (timeSinceLeave < MinTimeSinceLeaveMs)
        {
            int waitMs = MinTimeSinceLeaveMs - (int)timeSinceLeave;
            GameLogger.Log(LogSeverity.Debug, $"[QuickJoin] waiting {waitMs}ms cooldown");
            await UniTask.Delay(waitMs);
        }

        if (HasActiveSession)
        {
            GameLogger.Log(LogSeverity.Debug, "[QuickJoin] already has active session, aborting");
            return;
        }

        _isBusy = true;
        GameLogger.Log(LogSeverity.Debug, "[QuickJoin] _isBusy = true, querying sessions");
        bool shouldReturnToMenu = false;
        try
        {
            var sessions = (await QuerySessions()).ToList();
            GameLogger.Log(LogSeverity.Debug, $"[QuickJoin] found {sessions.Count} sessions");

            if (sessions.Count > 0)
            {
                GameLogger.Log(LogSeverity.Debug, $"[QuickJoin] joining session {sessions[0].Id}");
                try
                {
                    ActiveSession = await JoinSessionWithRetry(sessions[0].Id, 3);
                    GameLogger.Log(LogSeverity.Debug, "[QuickJoin] JoinSessionWithRetry SUCCESS");
                    OnSessionConnected(ActiveSession);
                    GameLogger.Log(LogSeverity.Info, $"[QuickJoin] Joined. Code: {ActiveSession.Code}");
                }
                catch (InvalidOperationException e)
                {
                    GameLogger.Log(LogSeverity.Warning, $"[QuickJoin] InvalidOperationException: {e.Message}");
                    await SafeLeaveAsync();
                    shouldReturnToMenu = true;
                }
                catch (SessionException e) when (e.Message.Contains("lobby not found") || e.Message.Contains("not found"))
                {
                    GameLogger.Log(LogSeverity.Warning, $"[QuickJoin] Session gone: {e.Message}");
                    await StartSessionAsHost();
                }
            }
            else
            {
                GameLogger.Log(LogSeverity.Debug, "[QuickJoin] no sessions, hosting");
                await StartSessionAsHost();
            }
        }
        catch (Exception e) when (e.Message.Contains("Unexpected exception processing network metadata"))
        {
            GameLogger.Log(LogSeverity.Error, $"[QuickJoin] METADATA EXCEPTION at retryCount={retryCount}: {e.Message}");
            if (retryCount < maxRetries)
            {
                _isBusy = false;
                await UniTask.Delay(2000);
                await QuickJoin(retryCount + 1);
                return;
            }
            GameLogger.Log(LogSeverity.Error, "[QuickJoin] Max retries hit, giving up.");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (SessionException e)
        {
            GameLogger.Log(LogSeverity.Error, $"[QuickJoin] SessionException: {e.GetType().FullName}: {e.Message}");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Error, "An unexpected error occurred.", e);
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        finally
        {
            _isBusy = false;
            GameLogger.Log(LogSeverity.Debug, "[QuickJoin] finally, _isBusy=false");
        }
        if (shouldReturnToMenu)
        {
            GameLogger.Log(LogSeverity.Debug, "[QuickJoin] returning to menu");
            PersistentGameStateManager.Instance.ReturnToMenu().Forget();
        }
        GameLogger.Log(LogSeverity.Debug, "[QuickJoin] END");
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
                    GameLogger.Log(LogSeverity.Warning, $"Service still cleaning up, retrying in 1s... (attempt {i + 1}/{maxRetries})");
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
                GameLogger.LogException(LogSeverity.Warning, "Session connection was lost.", e);
            }
            catch (Exception e)
            {
                GameLogger.LogException(LogSeverity.Error, "An unexpected error occurred while leaving the session.", e);
            }
            finally
            {
                ActiveSession = null;
                _lastLeaveTime = DateTime.UtcNow;
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
                ),
                new FilterOption(
                    FilterField.StringIndex1, Application.version, FilterOperation.Equal
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
            GameLogger.LogException(LogSeverity.Warning, "Failed to set session locked status.", e);
        }
    }

    public async UniTask LeaveSession()
    {
        if (_isBusy)
        {
            GameLogger.Log(LogSeverity.Warning, "LeaveSession called while busy. Ignoring.");
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
                    GameLogger.Log(LogSeverity.Warning, "Session state invalid.");
                    PersistentGameStateManager.Instance.ReturnToMenu().Forget();
                    break;
                }

                if (tickCount % 3 == 0)
                {
                    await ActiveSession.AsHost().RefreshAsync();
                    GameLogger.Log(LogSeverity.Debug, $"Session state after refresh: {ActiveSession.State}");
                }
            }
            catch (SessionException e)
            {
                GameLogger.LogException(LogSeverity.Warning, "An unexpected error occurred while refreshing the session.", e);
            }
        }
    }

    #endregion
}