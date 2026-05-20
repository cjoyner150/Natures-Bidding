using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Netcode;

public class MenuNetworkUI : MonoBehaviour
{
    private NetworkSessionManager sessionManager;

    private void Start()
    {
        PersistentGameStateManager.Instance.LoadingPanel.SetActive(false);
        sessionManager = NetworkSessionManager.Instance;
    }

    public async void JoinSessionByButton(TMP_InputField input)
    {
        PersistentGameStateManager.Instance.LoadingPanel.SetActive(true);
        bool validSession = await sessionManager.JoinSessionByCode(input.text);
        if (!validSession)
        {
            PersistentGameStateManager.Instance.LoadingPanel.SetActive(false);
        }
    }

    public async void QuickJoinByButton()
    {
        PersistentGameStateManager.Instance.LoadingPanel.SetActive(true);
        await sessionManager.QuickJoin();
    }

    public async void StartSessionAsHostByButton()
    {
        PersistentGameStateManager.Instance.LoadingPanel.SetActive(true);
        await sessionManager.StartSessionAsHost();
    }

    public void EnterNameByButton(TMP_InputField input)
    {
        _ = sessionManager.ChangePlayerName(input.text);
    }

    public async void QuitGameByButton()
    {
        await sessionManager.LeaveSession();
        Application.Quit();
    }
}
