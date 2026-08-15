using Cysharp.Threading.Tasks;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class BootstrapManager : MonoBehaviour
{

    

    async void Start()
    {
        PersistentGameStateManager.Instance.SetLoadingState("Initializing...");
#if !UNITY_EDITOR
        PersistentSteamManager.Instance.InitializeSteam();
#endif
        PersistentGameStateManager.Instance.SetLoadingState("Authenticating...");
        await NetworkSessionManager.Instance.WaitForAuth();

        PersistentGameStateManager.Instance.SetLoadingState("Loading...", true);
        await HandleCommandLineJoin();
    }

    private async UniTask HandleCommandLineJoin()
    {
        string[] args = Environment.GetCommandLineArgs();
        GameLogger.Log(LogSeverity.Debug, $"Command line args: {string.Join(", ", args)}");

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "+connect" && i + 1 < args.Length)
            {
                string sessionCode = args[i + 1];
                GameLogger.Log(LogSeverity.Debug, $"Launched with connect argument: {sessionCode}");

                await PersistentGameStateManager.Instance.LoadMenuScene();

                PersistentGameStateManager.Instance.SetLoadingState("Joining Session...");
                bool success = await NetworkSessionManager.Instance.JoinSessionByCode(sessionCode);
                if (!success)
                {
                    GameLogger.Log(LogSeverity.Warning, $"Failed to join session from command line: {sessionCode}");
                    PersistentGameStateManager.Instance.ClearLoadingState();
                }

                return;
            }
        }

        await PersistentGameStateManager.Instance.LoadMenuScene();
        PersistentGameStateManager.Instance.ClearLoadingState();
    }

    // Not technically necessary, but a safety belt because shutdown flows are inconsistent
    private void OnApplicationQuit()
    {
        if (SteamClient.IsValid)
            SteamClient.Shutdown();
    }
}
