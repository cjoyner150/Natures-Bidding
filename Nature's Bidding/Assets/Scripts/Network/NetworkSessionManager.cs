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
        base.Awake();

        DontDestroyOnLoad(gameObject);
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

        //players = await RequestPlayers();

    }

    public async UniTask ChangePlayerName(string playerName)
    {
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        Debug.Log($"Player updated with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
    }

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

        RegisterAuthData();
    }

    async UniTaskVoid JoinSessionByID(string sessionId)
    {
        ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
        Debug.Log($"Session with id: {sessionId} joined!");
        RegisterAuthData();
    }

    public async UniTask<bool> JoinSessionByCode(string sessionCode)
    {
        ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode);

        if (ActiveSession != null)
        {
            Debug.Log($"Session with id: {sessionCode} joined!");
            RegisterAuthData();
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
                Debug.Log($"Joined session. Code: {ActiveSession.Code}");
                RegisterAuthData();
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
        if (ActiveSession != null)
        {
            try
            {
                await ActiveSession.LeaveAsync();
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

    private void RegisterAuthData()
    {
        PlayerRegistry.Instance.Register(
            NetworkManager.Singleton.LocalClientId,
            AuthenticationService.Instance.PlayerId,
            AuthenticationService.Instance.PlayerName
        );

        SendAuthToServerRpc(
            AuthenticationService.Instance.PlayerId,
            AuthenticationService.Instance.PlayerName
        );
    }

    [Rpc(SendTo.Server)]
    public void SendAuthToServerRpc(string authId, string playerName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        PlayerRegistry.Instance.Register(clientId, authId, playerName);

        Debug.Log($"Auth received for {clientId}. GameplayServerHandler exists: {GameplayServerHandler.Instance != null}");

        // If gameplay is already running, spawn immediately
        if (GameplayServerHandler.Instance != null)
            GameplayServerHandler.Instance.SpawnAndRegisterPlayer(clientId);
    }

}
