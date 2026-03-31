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

public class NetworkSessionManager : Singleton<NetworkSessionManager>
{
    ISession activeSession;
    Dictionary<ulong, string> authenticationIdByClientId = new Dictionary<ulong, string>();
    Dictionary<ulong, string> playerNameByClientId = new Dictionary<ulong, string>();

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
            await AuthenticationService.Instance.UpdatePlayerNameAsync("Player");
            Debug.Log($"Player Initialized with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

    }

    public async UniTask ChangePlayerName(string playerName)
    {
        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
        Debug.Log($"Player updated with id: {AuthenticationService.Instance.PlayerId} and name: {AuthenticationService.Instance.PlayerName}");
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
    
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    public void RegisterClientIdRpc(ulong clientId, string clientAuthenticationId, string clientName)
    {
        if (authenticationIdByClientId.ContainsKey(clientId))
        {
            Debug.LogError($"Client with clientId {clientId} has already been registered.");
            return;
        }
        else if (authenticationIdByClientId.ContainsValue(clientAuthenticationId))
        {
            Debug.LogError($"Client with authId {clientAuthenticationId} has already been registered.");
            return;
        }
        
        authenticationIdByClientId.Add(clientId, clientAuthenticationId);
        playerNameByClientId.Add(clientId, clientName);
        
    }
    
    
    /// <summary>
    /// Client is not guaranteed to have updated dictionary on NetworkSessionManager. Dict is updated by RegisterClientIdRpc. Ensure Client has been registered.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public string RequestPlayerNameByClientId(ulong clientId)
    {
        return playerNameByClientId[clientId];
    }
    
    /// <summary>
    /// Client is not guaranteed to have updated dictionary on NetworkSessionManager. Dict is updated by RegisterClientIdRpc. Ensure Client has been registered.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public string RequestAuthenticationIdByClientId(ulong clientId)
    {
        return authenticationIdByClientId[clientId];
    }
}
