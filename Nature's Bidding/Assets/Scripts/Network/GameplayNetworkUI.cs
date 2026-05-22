using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Steamworks;
using UnityEngine.InputSystem;

public class GameplayNetworkUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI lobbyWaitingText;
    [SerializeField] InputAction playerReadyAction;
    bool readied = false;
    bool canReady = false;

    private NetworkSessionManager sessionManager;

    private void Start()
    {
        sessionManager = NetworkSessionManager.Instance;
        if (lobbyWaitingText != null) lobbyWaitingText.text = "Waiting on more players...";
    }

    private void OnEnable()
    {
        playerReadyAction.Enable();
        playerReadyAction.performed += OnPlayerReady;

        LobbyServerHandler.OnEnoughPlayersRegistered.AddListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.AddListener(OnPlayerRequirementDropped);
        CombatServerHandler.OnCombatBegin.AddListener(OnCombatBegin);
    }

    private void OnDisable()
    {
        playerReadyAction.Disable();
        playerReadyAction.performed -= OnPlayerReady;

        LobbyServerHandler.OnEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementMet);
        LobbyServerHandler.OnNoLongerEnoughPlayersRegistered.RemoveListener(OnPlayerRequirementDropped);
        CombatServerHandler.OnCombatBegin.RemoveListener(OnCombatBegin);
    }

    void OnPlayerReady(InputAction.CallbackContext ctx)
    {
        if (readied || !canReady) return;

        LobbyServerHandler.Instance.PlayerReadiedServerRpc();
        readied = true;

        lobbyWaitingText.text = "You are ready! Waiting on other players...";
    }

    void OnPlayerRequirementMet()
    {
        if (canReady || readied) return; // Client already knows the player requirement is met and should not have text reset

        lobbyWaitingText.text = "Waiting on players to ready up! Press START to ready up.";
        canReady = true;
    }

    void OnPlayerRequirementDropped()
    {
        lobbyWaitingText.text = "Waiting on more players...";
        readied = false;
        canReady = false;
    }

    void OnCombatBegin()
    {
        
    }

    public void LeaveSessionByButton()
    {
        LeaveSession();
    }

    public async void QuitGameByButton()
    {
        await sessionManager.LeaveSession();

        if (SteamClient.IsValid)
        {
            await PersistentSteamManager.Instance.ShutdownSteam();
        }

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
