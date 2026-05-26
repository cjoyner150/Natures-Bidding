using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Netcode;
using Steamworks;

public class MenuNetworkUI : MonoBehaviour
{
    private NetworkSessionManager sessionManager;

    private void Start()
    {
        sessionManager = NetworkSessionManager.Instance;
    }

    public async void JoinSessionByButton(TMP_InputField input)
    {
        if (PersistentGameStateManager.Instance.IsLoading) return;

        PersistentGameStateManager.Instance.IsLoading = true;
        PersistentGameStateManager.Instance.SetLoadingState("Validating Session...");

        bool validSession = await sessionManager.JoinSessionByCode(input.text);
        if (!validSession)
        {
            PersistentGameStateManager.Instance.LoadingPanel.SetActive(false);
        }
    }

    public async void QuickJoinByButton()
    {
        if (PersistentGameStateManager.Instance.IsLoading) return;

        PersistentGameStateManager.Instance.IsLoading = true;
        PersistentGameStateManager.Instance.SetLoadingState("Looking for sessions...");

        await sessionManager.QuickJoin();
    }

    public async void StartSessionAsHostByButton()
    {
        if (PersistentGameStateManager.Instance.IsLoading) return;

        PersistentGameStateManager.Instance.IsLoading = true;
        PersistentGameStateManager.Instance.SetLoadingState("Hosting session...");

        await sessionManager.StartSessionAsHost();
    }

    public void EnterNameByButton(TMP_InputField input)
    {
        _ = sessionManager.ChangePlayerName(input.text);
    }

    public async void QuitGameByButton()
    {
        if (PersistentGameStateManager.Instance.IsLoading) return;

        PersistentGameStateManager.Instance.IsLoading = true;
        PersistentGameStateManager.Instance.SetLoadingState("Ending session...");

        await sessionManager.LeaveSession();

        if (SteamClient.IsValid)
        {
            await PersistentSteamManager.Instance.ShutdownSteam();
        }

        Application.Quit();
    }
}
