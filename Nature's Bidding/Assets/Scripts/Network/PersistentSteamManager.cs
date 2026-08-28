using System;
using Cysharp.Threading.Tasks;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;
using static PersistentGameStateManager;

public class PersistentSteamManager : Singleton<PersistentSteamManager>
{

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }
    }

    public void InitializeSteam()
    {
#if !UNITY_EDITOR
    try
    {
        SteamClient.Init(5039210);
        GameLogger.Log(LogSeverity.Info, $"Steam initialized. Name: {SteamClient.Name}");
        SteamFriends.OnGameRichPresenceJoinRequested += OnFriendJoinRequested;
        Application.quitting += OnApplicationQuitting;
    }
    catch (Exception e)
    {
        GameLogger.Log(LogSeverity.Error, $"Steam failed to initialize: {e.Message}");
        // Steam isn't running — quit the game
        Application.Quit();
    }
#endif
    }

    private void Update()
    {
        #if !UNITY_EDITOR
        SteamClient.RunCallbacks();
        #endif
    }

    public void UpdateRichPresence(GameState state)
    {
        if (!SteamClient.IsValid) return;

        switch (state)
        {
            case GameState.Menu:
                SteamFriends.ClearRichPresence();
                break;

            case GameState.Lobby:
                string connectString = $"+connect {NetworkSessionManager.Instance.ActiveSession?.Code}";
                GameLogger.Log(LogSeverity.Debug, $"=== SetRichPresence ===");
                GameLogger.Log(LogSeverity.Debug, $"SteamId: {SteamClient.SteamId}");
                GameLogger.Log(LogSeverity.Debug, $"Setting connect: '{connectString}'");
                bool result = SteamFriends.SetRichPresence("connect", connectString);
                GameLogger.Log(LogSeverity.Debug, $"SetRichPresence result: {result}");
                SteamFriends.SetRichPresence("status", "In Lobby");
                break;

            case GameState.Shopping:
                SteamFriends.SetRichPresence("status", "In Shop");
                SteamFriends.SetRichPresence("connect", "");
                break;

            case GameState.Bidding:
                SteamFriends.SetRichPresence("status", "Bidding");
                SteamFriends.SetRichPresence("connect", "");
                break;

            case GameState.Combat:
                SteamFriends.SetRichPresence("status", "In Game");
                SteamFriends.SetRichPresence("connect", "");
                break;
        }
    }

    private bool _isHandlingFriendJoin = false;

    private void OnFriendJoinRequested(Friend friend, string connect)
    {
        GameLogger.Log(LogSeverity.Debug, $"=== OnFriendJoinRequested ===");
        GameLogger.Log(LogSeverity.Debug, $"Friend Name: {friend.Name}");
        GameLogger.Log(LogSeverity.Debug, $"Friend SteamId: {friend.Id}");
        GameLogger.Log(LogSeverity.Debug, $"Local SteamId: {SteamClient.SteamId}");
        GameLogger.Log(LogSeverity.Debug, $"Connect string received: '{connect}'");
        GameLogger.Log(LogSeverity.Debug, $"Our own Rich Presence connect: '{SteamFriends.GetRichPresence("connect")}'");

        // Check what Rich Presence the friend actually has
        string friendConnect = friend.GetRichPresence("connect");
        string friendStatus = friend.GetRichPresence("status");
        GameLogger.Log(LogSeverity.Debug, $"Friend's Rich Presence connect: '{friendConnect}'");
        GameLogger.Log(LogSeverity.Debug, $"Friend's Rich Presence status: '{friendStatus}'");

        if (_isHandlingFriendJoin)
        {
            GameLogger.Log(LogSeverity.Warning, "HandleFriendJoin already in progress, ignoring.");
            return;
        }
        _isHandlingFriendJoin = true;

        string sessionCode = connect.Replace("+connect ", "").Trim();
        GameLogger.Log(LogSeverity.Debug, $"Parsed session code: '{sessionCode}'");
        HandleFriendJoin(sessionCode).Forget();
    }

    private async UniTaskVoid HandleFriendJoin(string sessionCode)
    {
        try
        {
            GameLogger.Log(LogSeverity.Debug, $"HandleFriendJoin started. SessionCode: '{sessionCode}'");
            GameLogger.Log(LogSeverity.Debug, $"  IsReturningToMenu: {PersistentGameStateManager.Instance.IsReturningToMenu}");
            GameLogger.Log(LogSeverity.Debug, $"  IsBusy: {NetworkSessionManager.Instance.IsBusy}");
            GameLogger.Log(LogSeverity.Debug, $"  HasActiveSession: {NetworkSessionManager.Instance.HasActiveSession}");
            GameLogger.Log(LogSeverity.Debug, $"  GameState: {PersistentGameStateManager.Instance.State}");
            GameLogger.Log(LogSeverity.Debug, $"  NetworkManager exists: {NetworkManager.Singleton != null}");
            GameLogger.Log(LogSeverity.Debug, $"  NetworkManager IsListening: {NetworkManager.Singleton?.IsListening}");

            int waitFrame = 0;
            await UniTask.WaitUntil(() =>
            {
                bool returningToMenu = PersistentGameStateManager.Instance.IsReturningToMenu;
                bool busy = NetworkSessionManager.Instance.IsBusy;

                if (waitFrame++ % 60 == 0) // Log every 60 frames if still waiting
                    GameLogger.Log(LogSeverity.Debug, $"HandleFriendJoin waiting... IsReturningToMenu: {returningToMenu}, IsBusy: {busy}");

                return !returningToMenu && !busy;
            });

            GameLogger.Log(LogSeverity.Debug, "HandleFriendJoin wait complete — proceeding.");
            GameLogger.Log(LogSeverity.Debug, $"  HasActiveSession: {NetworkSessionManager.Instance.HasActiveSession}");

            if (NetworkSessionManager.Instance.HasActiveSession)
            {
                GameLogger.Log(LogSeverity.Debug, "Has active session — calling ReturnToMenu before joining.");
                PersistentGameStateManager.Instance.ReturnToMenu().Forget();

                waitFrame = 0;
                await UniTask.WaitUntil(() =>
                {
                    bool returningToMenu = PersistentGameStateManager.Instance.IsReturningToMenu;
                    bool hasSession = NetworkSessionManager.Instance.HasActiveSession;

                    if (waitFrame++ % 60 == 0)
                        GameLogger.Log(LogSeverity.Debug, $"Waiting for ReturnToMenu... IsReturningToMenu: {returningToMenu}, HasActiveSession: {hasSession}");

                    return !returningToMenu && !hasSession;
                });

                GameLogger.Log(LogSeverity.Debug, "ReturnToMenu complete.");
            }

            GameLogger.Log(LogSeverity.Debug, $"Attempting to join session: '{sessionCode}'");
            bool success = await NetworkSessionManager.Instance.JoinSessionByCode(sessionCode);
            GameLogger.Log(LogSeverity.Debug, $"JoinSessionByCode result: {success}");

            if (success)
            {
                GameLogger.Log(LogSeverity.Debug, "Join successful — scene loaded via Netcode sync, OnGameplaySceneReady will handle UI.");
            } else GameLogger.Log(LogSeverity.Warning, $"Failed to join friend's session: {sessionCode}");
        }
        catch (Exception e)
        {
            GameLogger.LogException(LogSeverity.Error, "Exception occurred in HandleFriendJoin.", e);
        }
        finally
        {
            GameLogger.Log(LogSeverity.Debug, "HandleFriendJoin complete — resetting _isHandlingFriendJoin.");
            _isHandlingFriendJoin = false;
        }
    }

    private async void OnApplicationQuitting()
    {
        GameLogger.Log(LogSeverity.Debug, $"OnApplicationQuitting. IsHandlingFriendJoin: {_isHandlingFriendJoin}, SteamClient.IsValid: {SteamClient.IsValid}");
        if (SteamClient.IsValid)
        {
            GameLogger.Log(LogSeverity.Debug, "Calling SteamClient.Shutdown() in OnApplicationQuitting.");
            await ShutdownSteam();
        }
    }

    public async UniTask ShutdownSteam()
    {
        SteamFriends.OnGameRichPresenceJoinRequested -= OnFriendJoinRequested;
        Application.quitting -= OnApplicationQuitting;
        SteamClient.Shutdown();

        await UniTask.Delay(500);

        GameLogger.Log(LogSeverity.Debug, "Steam Shutdown Complete.");
    }

}
