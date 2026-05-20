using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public class GameplayNetworkUI : MonoBehaviour
{
    private NetworkSessionManager sessionManager;

    private void Start()
    {
        sessionManager = NetworkSessionManager.Instance;
    }

    public void LeaveSessionByButton()
    {
        LeaveSession();
    }

    public async void QuitGameByButton()
    {
        await sessionManager.LeaveSession();
        Application.Quit();
    }

    public void LeaveSession()
    {
        PlayerPauseManager.Instance.ForceResume();

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        PersistentGameStateManager.Instance.ReturnToMenu();
    }
}
