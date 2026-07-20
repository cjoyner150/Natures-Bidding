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
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class NetworkSessionManager : Singleton<NetworkSessionManager>
{
    private const bool EnableSessionDebugLogging = false;

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

    private DateTime _lastLeaveTime = DateTime.MinValue;
    private const int MinTimeSinceLeaveMs = 2000;

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            if (EnableSessionDebugLogging)
                File.WriteAllText(LogFilePath, $"=== Session started {DateTime.Now} ===\n");
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
        }

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromHost;
        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;

        StopLobbyHeartbeat();
    }

    private void OnTransportFailure()
    {
        Debug.Log("Unity Transport Failed.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnSessionDeleted()
    {
        Debug.Log("Session deleted by host.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnRemovedFromSession()
    {
        Debug.Log("Removed from session.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
    }

    private void OnDisconnectedFromHost(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer) return;
        if (_isBusy) return;
        Debug.Log("Disconnected from host.");
        _ = PersistentGameStateManager.Instance.ReturnToMenu();
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
        WriteLog("[StartSessionAsHost] START");
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
                WriteLog($"[StartSessionAsHost] Session created. Id: {ActiveSession.Id}, Code: {ActiveSession.Code}");
                OnSessionConnected(ActiveSession);
                WriteLog("[StartSessionAsHost] OnSessionConnected complete, returning");
                return;
            }
            catch (SessionException e) when (e.Message.Contains("fetch relay join code") || e.Message.Contains("timeout"))
            {
                WriteLog($"[StartSessionAsHost] Retry-worthy exception: {e.Message}");
                if (i < maxRetries - 1)
                {
                    WriteLog($"[StartSessionAsHost] retrying attempt {i + 1}/{maxRetries}");
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

        WriteLog($"[QuickJoin] START retryCount={retryCount}, _isBusy={_isBusy}");
        await UniTask.WaitUntil(() => !_isBusy && !PersistentGameStateManager.Instance.IsReturningToMenu);
        WriteLog("[QuickJoin] passed busy/returning wait");

        var timeSinceLeave = (DateTime.UtcNow - _lastLeaveTime).TotalMilliseconds;
        WriteLog($"[QuickJoin] timeSinceLeave={timeSinceLeave}ms");
        if (timeSinceLeave < MinTimeSinceLeaveMs)
        {
            int waitMs = MinTimeSinceLeaveMs - (int)timeSinceLeave;
            WriteLog($"[QuickJoin] waiting {waitMs}ms cooldown");
            await UniTask.Delay(waitMs);
        }

        if (HasActiveSession)
        {
            WriteLog("[QuickJoin] already has active session, aborting");
            return;
        }

        _isBusy = true;
        WriteLog("[QuickJoin] _isBusy = true, querying sessions");
        bool shouldReturnToMenu = false;
        try
        {
            var sessions = (await QuerySessions()).ToList();
            WriteLog($"[QuickJoin] found {sessions.Count} sessions");

            if (sessions.Count > 0)
            {
                WriteLog($"[QuickJoin] joining session {sessions[0].Id}");
                try
                {
                    ActiveSession = await JoinSessionWithRetry(sessions[0].Id, 3);
                    WriteLog("[QuickJoin] JoinSessionWithRetry SUCCESS");
                    OnSessionConnected(ActiveSession);
                    WriteLog($"[QuickJoin] Joined. Code: {ActiveSession.Code}");
                }
                catch (InvalidOperationException e)
                {
                    WriteLog($"[QuickJoin] InvalidOperationException: {e.Message}");
                    await SafeLeaveAsync();
                    shouldReturnToMenu = true;
                }
                catch (SessionException e) when (e.Message.Contains("lobby not found") || e.Message.Contains("not found"))
                {
                    WriteLog($"[QuickJoin] Session gone: {e.Message}");
                    await StartSessionAsHost();
                }
            }
            else
            {
                WriteLog("[QuickJoin] no sessions, hosting");
                await StartSessionAsHost();
            }
        }
        catch (Exception e) when (e.Message.Contains("Unexpected exception processing network metadata"))
        {
            WriteLog($"[QuickJoin] METADATA EXCEPTION at retryCount={retryCount}: {e.Message}");
            if (retryCount < maxRetries)
            {
                _isBusy = false;
                await UniTask.Delay(2000);
                await QuickJoin(retryCount + 1);
                return;
            }
            WriteLog("[QuickJoin] Max retries hit, giving up.");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (SessionException e)
        {
            WriteLog($"[QuickJoin] SessionException: {e.GetType().FullName}: {e.Message}");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        catch (Exception e)
        {
            WriteLog($"[QuickJoin] Exception: {e.GetType().FullName}: {e.Message}\n{e.StackTrace}");
            ActiveSession = null;
            shouldReturnToMenu = true;
        }
        finally
        {
            _isBusy = false;
            WriteLog("[QuickJoin] finally, _isBusy=false");
        }
        if (shouldReturnToMenu)
        {
            WriteLog("[QuickJoin] returning to menu");
            PersistentGameStateManager.Instance.ReturnToMenu();
        }
        WriteLog("[QuickJoin] END");
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
                Debug.LogWarning($"Session leave warning (non-fatal): {e.Message}");
            }
            catch (Exception e)
            {
                Debug.Log($"Exception type: {e.GetType().FullName}, Message: {e.Message}");
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
    private static string LogFilePath => Path.Combine(
    Application.persistentDataPath,
    $"session_debug_pid{Process.GetCurrentProcess().Id}.log"
    );

    private void WriteLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.Log(line);
        File.AppendAllText(LogFilePath, line + "\n");
    }
}
