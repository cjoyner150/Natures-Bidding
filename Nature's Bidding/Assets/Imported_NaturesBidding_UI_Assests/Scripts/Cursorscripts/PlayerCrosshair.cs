using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerCrosshair : NetworkBehaviour
{
    private NetworkVariable<Vector2> _normalizedMousePos = new NetworkVariable<Vector2>(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Image _cursorImage;
    private bool _cursorPaused = false;

    private static List<PlayerCrosshair> _allInstances = new List<PlayerCrosshair>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerCrosshair] OnNetworkSpawn for client {OwnerClientId}, IsLocal={IsLocalPlayer}");

        if (IsServer)
        {
            _allInstances.Add(this);
            // Assign a unique color index based on connection order (0,1,2,3)
            int index = _allInstances.Count - 1;
            _colorIndex.Value = index;
            CursorManager.Instance?.AssignColorIndex(OwnerClientId, index);

            if (_allInstances.Count == 1)
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        if (CursorManager.Instance == null)
            StartCoroutine(RetryCreateCursor());
        else
            CreateCursor();

        _normalizedMousePos.OnValueChanged += (oldPos, newPos) =>
        {
            if (_cursorImage != null && !IsLocalPlayer)
            {
                Vector2 screenPos = new Vector2(newPos.x * Screen.width, newPos.y * Screen.height);
                _cursorImage.rectTransform.anchoredPosition = screenPos;
            }
        };
    }

    private void OnClientConnected(ulong newClientId)
    {
        if (!IsServer) return;
        StartCoroutine(DelayedSyncToNewClient(newClientId));
    }

    private IEnumerator DelayedSyncToNewClient(ulong newClientId)
    {
        yield return new WaitForSeconds(0.5f);
        foreach (var pc in _allInstances)
        {
            if (pc == null || pc.OwnerClientId == newClientId) continue;
            pc.SendCurrentPositionToClientRpc(pc._normalizedMousePos.Value, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
            });
        }
    }

    [ClientRpc]
    private void SendCurrentPositionToClientRpc(Vector2 pos, ClientRpcParams rpcParams = default)
    {
        if (_cursorImage != null)
        {
            Vector2 screenPos = new Vector2(pos.x * Screen.width, pos.y * Screen.height);
            _cursorImage.rectTransform.anchoredPosition = screenPos;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            _allInstances.Remove(this);
            if (_allInstances.Count == 0)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        base.OnNetworkDespawn();
    }

    private IEnumerator RetryCreateCursor()
    {
        float timeout = 2f;
        float start = Time.time;
        while (CursorManager.Instance == null && Time.time - start < timeout)
        {
            yield return new WaitForSeconds(0.2f);
        }
        if (CursorManager.Instance != null)
            CreateCursor();
        else
        {
            Debug.LogError("CursorManager still missing after timeout.");
            enabled = false;
        }
    }

    private void CreateCursor()
    {
        if (CursorManager.Instance == null || !CursorManager.Instance.cursorEnabled) return;
        if (CursorManager.Instance.cursorUIPrefab == null) { enabled = false; return; }

        if (IsLocalPlayer) Cursor.visible = false;

        Canvas canvas = CursorManager.Instance.GetCursorCanvas();
        if (canvas == null) { enabled = false; return; }

        GameObject go = Instantiate(CursorManager.Instance.cursorUIPrefab, canvas.transform);
        _cursorImage = go.GetComponent<Image>();
        if (_cursorImage == null) { Destroy(go); enabled = false; return; }

        RectTransform rt = _cursorImage.rectTransform;
        rt.pivot = new Vector2(0, 1);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // Use the color index assigned by server
        Color playerColor = CursorManager.Instance.GetColorForPlayer(OwnerClientId);
        _cursorImage.color = playerColor;
        Debug.Log($"Cursor created for player {OwnerClientId} with color {playerColor} (index {_colorIndex.Value})");

        if (!IsLocalPlayer) enabled = false;
    }

    private void Update()
    {
        if (!IsLocalPlayer) return;

        if (_cursorImage == null)
        {
            if (CursorManager.Instance != null && CursorManager.Instance.cursorEnabled)
                CreateCursor();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            _cursorPaused = !_cursorPaused;
            Debug.Log($"Cursor pause: {(_cursorPaused ? "ON" : "OFF")}");
        }

        if (CursorManager.Instance == null || !CursorManager.Instance.cursorEnabled)
        {
            if (_cursorImage != null) _cursorImage.gameObject.SetActive(false);
            return;
        }

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if (!_cursorPaused)
        {
            if (_cursorImage != null)
                _cursorImage.rectTransform.anchoredPosition = mousePos;
            Vector2 normPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);
            _normalizedMousePos.Value = normPos;
        }
    }

    public override void OnDestroy()
    {
        if (_cursorImage != null) Destroy(_cursorImage.gameObject);
        if (IsLocalPlayer && CursorManager.Instance != null && CursorManager.Instance.cursorEnabled)
            Cursor.visible = true;
    }
}