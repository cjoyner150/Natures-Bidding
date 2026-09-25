using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityUtils;

public class CursorUIManager : Singleton<CursorUIManager>
{

    [Header("Global Toggle for Testing")]
    public bool cursorEnabled = true;

    [Header("Cursor Visual")]
    [SerializeField] private GameObject playerCursorPrefab;

    [Header("Player Colors (Hardcoded for up to 4 players)")]
    [SerializeField] private List<Color> playerColors;

    private Canvas _cursorCanvas;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        GameLogger.Log(LogSeverity.Debug, "Initialized.");
        CreateCursorCanvas();
    }

    private void CreateCursorCanvas()
    {
        GameObject canvasGO = GameObject.Find("CursorCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("CursorCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            canvasGO.AddComponent<CanvasScaler>();
        }
        _cursorCanvas = canvasGO.GetComponent<Canvas>();
    }

    public Canvas GetCursorCanvas()
    {
        if (_cursorCanvas == null) CreateCursorCanvas();
        return _cursorCanvas;
    }

    /// <summary>Get color for a player. Uses assigned index if exists, otherwise falls back to hash.</summary>
    public async UniTask<Color> GetColorForPlayer(ulong clientId)
    {
        if (playerColors.Count < PersistentPlayerRegistry.Instance.GetAllPlayers().Count)
        {
            GameLogger.Log(LogSeverity.Error, "[CursorUIManager] Not enough player colors defined for the number of players. Please add more colors to the playerColors list.");
            return Color.white;
        }

        int retries = 10;
        await UniTask.WaitUntil(() => {
            retries--;
            if (retries <= 0)
                return true;
            return PersistentPlayerRegistry.Instance != null && PersistentPlayerRegistry.Instance.GetByClientId(clientId) != null;
        });

        var player = PersistentPlayerRegistry.Instance.GetByClientId(clientId);
        if (player != null)
        {
            return playerColors[player.playerIndex];
        }

        GameLogger.Log(LogSeverity.Error, "No index assigned for player: " + clientId);
        // Fallback (should not happen if assigned properly)
        int fallbackIdx = (int)(clientId % (ulong)playerColors.Count);
        return playerColors[fallbackIdx];
    }

    public bool CheckCursorReady()
    {
        if (!cursorEnabled) return false;
        if (playerCursorPrefab == null) { return false; }

        return true;
    }

    public RectTransform SpawnCursor(bool syncCursorPosition, out Image _cursorImage)
    {
        GameObject prefab = playerCursorPrefab;

        GameObject go = Instantiate(prefab, GetCursorCanvas().transform);
        _cursorImage = go.GetComponentInChildren<Image>();
        if (_cursorImage == null) 
        { 
            Destroy(go); 
            GameLogger.Log(LogSeverity.Error, "Cursor prefab does not have an Image component.");
            return null;
        }
        return go.GetComponent<RectTransform>();
    }

    #region Local (non-networked) cursor for phases without a live player object (e.g. Map)

    private Image _localCursorImage;
    private VirtualMouseInput _localVirtualMouseInput;
    private bool _localCursorEnabled;

    /// <summary>Shows/hides the fancy local-player cursor for scenes that have no spawned player object (e.g. Map).</summary>
    public async void SetLocalCursorEnabled(bool enable)
    {
        if (!enable && _localCursorImage == null) return; // nothing spawned yet, nothing to disable

        if (_localCursorImage == null)
        {
            EnsureLocalCursorSpawned();
            await UniTask.WaitUntil(() => _localCursorImage != null);
        }

        _localCursorEnabled = enable;

        if (enable)
        {
            Cursor.lockState = CursorLockMode.Confined;
            var mousePos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (_localVirtualMouseInput != null && _localVirtualMouseInput.virtualMouse != null)
                UnityEngine.InputSystem.LowLevel.InputState.Change(_localVirtualMouseInput.virtualMouse.position, mousePos);

            _localCursorImage.rectTransform.anchoredPosition = mousePos;
            _localCursorImage.enabled = true;
        }
        else
        {
            _localCursorImage.enabled = false;
        }
    }

    private async void EnsureLocalCursorSpawned()
    {
        if (_localCursorImage != null) return;

        RectTransform rt = SpawnCursor(syncCursorPosition: false, out Image image);
        if (image == null) return;

        _localCursorImage = image;
        _localVirtualMouseInput = rt.GetComponent<VirtualMouseInput>();

        Color playerColor = await GetColorForPlayer(Unity.Netcode.NetworkManager.Singleton.LocalClientId);
        _localCursorImage.color = playerColor;

        rt.pivot = new Vector2(0, 1);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        _localCursorImage.enabled = _localCursorEnabled;
    }

    #endregion
}