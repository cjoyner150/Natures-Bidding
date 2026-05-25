using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LobbyManager — Handles Host/Join canvas and starting the game.
/// Attach to a GameObject in LobbyScene.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Canvases")]
    public GameObject mainMenuCanvas;
    public GameObject hostCanvas;
    public GameObject joinCanvas;
    public GameObject lobbyWaitCanvas;

    [Header("Input Fields")]
    public TMP_InputField joinCodeInput;
    public TMP_InputField playerNameInput;

    [Header("Lobby Wait UI")]
    public Transform playerListContainer;
    public GameObject playerListItemPrefab;
    public TMP_Text statusText;
    public Button startGameButton;

    [Header("Settings")]
    public int minPlayersToStart = 2;
    public int maxPlayers        = 4;

    #endregion

    #region Lifecycle

    void Start()
    {
        ShowMainMenu();

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[LobbyManager] NetworkManager not found in scene!");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback  += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #endregion

    #region Canvas Navigation

    public void ShowMainMenu()
    {
        SetAllCanvasesInactive();
        mainMenuCanvas.SetActive(true);
    }

    public void ShowHostMenu()
    {
        SetAllCanvasesInactive();
        hostCanvas.SetActive(true);
    }

    public void ShowJoinMenu()
    {
        SetAllCanvasesInactive();
        joinCanvas.SetActive(true);
    }

    void SetAllCanvasesInactive()
    {
        mainMenuCanvas?.SetActive(false);
        hostCanvas?.SetActive(false);
        joinCanvas?.SetActive(false);
        lobbyWaitCanvas?.SetActive(false);
    }

    #endregion

    #region Host / Join / Leave

    public void OnHostGame()
    {
        PlayerPrefs.SetString("PlayerName", playerNameInput.text == "" ? "Host" : playerNameInput.text);
        NetworkManager.Singleton.StartHost();
        EnterLobbyWait();
    }

    public void OnJoinGame()
    {
        PlayerPrefs.SetString("PlayerName", playerNameInput.text == "" ? "Player" : playerNameInput.text);
        NetworkManager.Singleton.StartClient();
        EnterLobbyWait();
    }

    public void OnLeave()
    {
        NetworkManager.Singleton.Shutdown();
        ShowMainMenu();
    }

    #endregion

    #region Start Game

    public void OnStartGame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        int connected = NetworkManager.Singleton.ConnectedClients.Count;
        if (connected < minPlayersToStart)
        {
            statusText.text = $"Need at least {minPlayersToStart} players. ({connected} connected)";
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene("Bidding_Scene",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    #endregion

    #region Lobby Wait

    void EnterLobbyWait()
    {
        SetAllCanvasesInactive();
        lobbyWaitCanvas.SetActive(true);
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        RefreshPlayerList();
    }

    void OnClientConnected(ulong clientId)
    {
        RefreshPlayerList();
        UpdateStatusText();
    }

    void OnClientDisconnected(ulong clientId)
    {
        RefreshPlayerList();
        UpdateStatusText();
    }

    void RefreshPlayerList()
    {
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            var item  = Instantiate(playerListItemPrefab, playerListContainer);
            var label = item.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"Player {kvp.Key}";
        }
    }

    void UpdateStatusText()
    {
        int count = NetworkManager.Singleton.ConnectedClients.Count;
        statusText.text = $"Players: {count}/{maxPlayers}";
    }

    #endregion
}