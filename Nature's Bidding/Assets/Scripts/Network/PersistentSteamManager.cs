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
        SteamClient.Init(4462510);
        Debug.Log($"Steam initialized. Name: {SteamClient.Name}");
        SteamFriends.OnGameRichPresenceJoinRequested += OnFriendJoinRequested;
        Application.quitting += OnApplicationQuitting;
    }
    catch (Exception e)
    {
        Debug.LogError($"Steam failed to initialize: {e.Message}");
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
                Debug.Log($"=== SetRichPresence ===");
                Debug.Log($"SteamId: {SteamClient.SteamId}");
                Debug.Log($"Setting connect: '{connectString}'");
                bool result = SteamFriends.SetRichPresence("connect", connectString);
                Debug.Log($"SetRichPresence result: {result}");
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
        Debug.Log($"=== OnFriendJoinRequested ===");
        Debug.Log($"Friend Name: {friend.Name}");
        Debug.Log($"Friend SteamId: {friend.Id}");
        Debug.Log($"Local SteamId: {SteamClient.SteamId}");
        Debug.Log($"Connect string received: '{connect}'");
        Debug.Log($"Our own Rich Presence connect: '{SteamFriends.GetRichPresence("connect")}'");

        // Check what Rich Presence the friend actually has
        string friendConnect = friend.GetRichPresence("connect");
        string friendStatus = friend.GetRichPresence("status");
        Debug.Log($"Friend's Rich Presence connect: '{friendConnect}'");
        Debug.Log($"Friend's Rich Presence status: '{friendStatus}'");

        if (_isHandlingFriendJoin)
        {
            Debug.LogWarning("HandleFriendJoin already in progress, ignoring.");
            return;
        }
        _isHandlingFriendJoin = true;

        string sessionCode = connect.Replace("+connect ", "").Trim();
        Debug.Log($"Parsed session code: '{sessionCode}'");
        HandleFriendJoin(sessionCode).Forget();
    }

    private async UniTaskVoid HandleFriendJoin(string sessionCode)
    {
        try
        {
            Debug.Log($"HandleFriendJoin started. SessionCode: '{sessionCode}'");
            Debug.Log($"  IsReturningToMenu: {PersistentGameStateManager.Instance.IsReturningToMenu}");
            Debug.Log($"  IsBusy: {NetworkSessionManager.Instance.IsBusy}");
            Debug.Log($"  HasActiveSession: {NetworkSessionManager.Instance.HasActiveSession}");
            Debug.Log($"  GameState: {PersistentGameStateManager.Instance.State}");
            Debug.Log($"  NetworkManager exists: {NetworkManager.Singleton != null}");
            Debug.Log($"  NetworkManager IsListening: {NetworkManager.Singleton?.IsListening}");

            int waitFrame = 0;
            await UniTask.WaitUntil(() =>
            {
                bool returningToMenu = PersistentGameStateManager.Instance.IsReturningToMenu;
                bool busy = NetworkSessionManager.Instance.IsBusy;

                if (waitFrame++ % 60 == 0) // Log every 60 frames if still waiting
                    Debug.Log($"HandleFriendJoin waiting... IsReturningToMenu: {returningToMenu}, IsBusy: {busy}");

                return !returningToMenu && !busy;
            });

            Debug.Log("HandleFriendJoin wait complete — proceeding.");
            Debug.Log($"  HasActiveSession: {NetworkSessionManager.Instance.HasActiveSession}");

            if (NetworkSessionManager.Instance.HasActiveSession)
            {
                Debug.Log("Has active session — calling ReturnToMenu before joining.");
                PersistentGameStateManager.Instance.ReturnToMenu();

                waitFrame = 0;
                await UniTask.WaitUntil(() =>
                {
                    bool returningToMenu = PersistentGameStateManager.Instance.IsReturningToMenu;
                    bool hasSession = NetworkSessionManager.Instance.HasActiveSession;

                    if (waitFrame++ % 60 == 0)
                        Debug.Log($"Waiting for ReturnToMenu... IsReturningToMenu: {returningToMenu}, HasActiveSession: {hasSession}");

                    return !returningToMenu && !hasSession;
                });

                Debug.Log("ReturnToMenu complete.");
            }

            Debug.Log($"Attempting to join session: '{sessionCode}'");
            bool success = await NetworkSessionManager.Instance.JoinSessionByCode(sessionCode);
            Debug.Log($"JoinSessionByCode result: {success}");

            if (success)
            {
                Debug.Log("Join successful — scene loaded via Netcode sync, OnGameplaySceneReady will handle UI.");
            } else Debug.LogWarning($"Failed to join friend's session: {sessionCode}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            Debug.Log("HandleFriendJoin complete — resetting _isHandlingFriendJoin.");
            _isHandlingFriendJoin = false;
        }
    }

    private async void OnApplicationQuitting()
    {
        Debug.Log($"OnApplicationQuitting. IsHandlingFriendJoin: {_isHandlingFriendJoin}, SteamClient.IsValid: {SteamClient.IsValid}");
        if (SteamClient.IsValid)
        {
            Debug.Log("Calling SteamClient.Shutdown() in OnApplicationQuitting.");
            await ShutdownSteam();
        }
    }

    public async UniTask ShutdownSteam()
    {
        SteamFriends.OnGameRichPresenceJoinRequested -= OnFriendJoinRequested;
        Application.quitting -= OnApplicationQuitting;
        SteamClient.Shutdown();

        await UniTask.Delay(500);

        Debug.Log("Steam Shutdown Complete.");
    }

}
