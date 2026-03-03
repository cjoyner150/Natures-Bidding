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

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Player Initialized with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

    }

    public async void StartSessionAsHost()
    {
        var options = new SessionOptions
        {
            MaxPlayers = 4,
            IsPrivate = false,
            IsLocked = false,
        }.WithRelayNetwork();

        ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(options);
        Debug.Log($"Session started with id: {ActiveSession.Id}, and join code: {ActiveSession.Code}");
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
            Debug.Log($"Session with id: {sessionCode} joined!");
            return true;
        }
        else return false;
    }

    public async UniTaskVoid QuickJoin()
    {
        var sessions = (await QuerySessions()).ToList();
        if (sessions.Count > 0)
        {
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessions[0].Id);
            Debug.Log($"Session with code: {ActiveSession.Code} joined!");
        }
        else
        {
            StartSessionAsHost();
        }
    }

    async UniTaskVoid KickPlayer(string playerId)
    {
        if (!activeSession.IsHost) return;

        await ActiveSession.AsHost().RemovePlayerAsync(playerId);
    }

    async UniTask<IList<ISessionInfo>> QuerySessions()
    {
        var options = new QuerySessionsOptions();

        var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
        return results.Sessions;
    }

    async UniTaskVoid LeaveSession()
    {
        if (ActiveSession != null)
        {
            try
            {
                await ActiveSession.LeaveAsync();
            }
            catch {
            
            }
            finally
            {
                ActiveSession = null;
            }
        }
    }
}
