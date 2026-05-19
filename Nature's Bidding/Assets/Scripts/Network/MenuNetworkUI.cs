using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Netcode;

public class MenuNetworkUI : MonoBehaviour
{
    [SerializeField] GameObject loadingPanel;
    [SerializeField] TextMeshProUGUI loadingProgress;
    private NetworkSessionManager sessionManager;

    private void Start()
    {
        loadingPanel.SetActive(false);
        sessionManager = NetworkSessionManager.Instance;
    }

    public async void JoinSessionByButton(TMP_InputField input)
    {
        loadingPanel.SetActive(true);
        bool validSession = await sessionManager.JoinSessionByCode(input.text);
        if (validSession)
        {
            await LoadSceneAsync(1);
        }
        else loadingPanel.SetActive(false);
    }

    public async void QuickJoinByButton()
    {
        loadingPanel.SetActive(true);
        await sessionManager.QuickJoin();
        await LoadSceneAsync(1);
    }

    public async void StartSessionAsHostByButton()
    {
        loadingPanel.SetActive(true);
        await sessionManager.StartSessionAsHost();
        await LoadSceneAsync(1);
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

    private async UniTask LoadSceneAsync(int idx)
    {
        if (!loadingPanel.activeSelf) loadingPanel.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(idx);

        while (op.progress < 0.95f)
        {
            loadingProgress.text = $"{op.progress.ToString("F1")}%";
            await UniTask.Delay(50);
        }

        loadingProgress.text = "100%";

        await UniTask.Delay(200);

        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(idx));   
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

}
