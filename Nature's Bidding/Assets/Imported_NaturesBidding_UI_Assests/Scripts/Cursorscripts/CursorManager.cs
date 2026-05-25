using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Global Toggle for Testing")]
    public bool cursorEnabled = true;

    [Header("Cursor Visual")]
    public GameObject cursorUIPrefab;

    [Header("Player Colors (Hardcoded for up to 4 players)")]
    public Color[] playerColors = new Color[]
    {
        Color.green,
        Color.cyan,
        Color.magenta,
        Color.yellow
    };

    private Canvas _cursorCanvas;
    private Dictionary<ulong, int> _playerColorIndex = new Dictionary<ulong, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[CursorManager] Initialized.");
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
            DontDestroyOnLoad(canvasGO);
        }
        _cursorCanvas = canvasGO.GetComponent<Canvas>();
    }

    public Canvas GetCursorCanvas()
    {
        if (_cursorCanvas == null) CreateCursorCanvas();
        return _cursorCanvas;
    }

    /// <summary>Call this on the server when a player spawns.</summary>
    public void AssignColorIndex(ulong clientId, int index)
    {
        _playerColorIndex[clientId] = index;
    }

    /// <summary>Get color for a player. Uses assigned index if exists, otherwise falls back to hash.</summary>
    public Color GetColorForPlayer(ulong clientId)
    {
        if (_playerColorIndex.TryGetValue(clientId, out int idx))
        {
            idx = idx % playerColors.Length;
            return playerColors[idx];
        }
        // Fallback (should not happen if assigned properly)
        int fallbackIdx = (int)(clientId % (ulong)playerColors.Length);
        return playerColors[fallbackIdx];
    }
}