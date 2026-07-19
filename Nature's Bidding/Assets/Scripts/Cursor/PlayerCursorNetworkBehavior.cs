using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class PlayerCursorNetworkBehavior : NetworkBehaviour
{
    [SerializeField] private bool spawnNetworkedCursor = true;

    private NetworkVariable<Vector2> _normalizedCursorPos = new NetworkVariable<Vector2>(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private RectTransform cursorTransform;
    private CursorInputHandler cursorInput;
    private Image cursorImage;

    private static List<PlayerCursorNetworkBehavior> _allInstances = new List<PlayerCursorNetworkBehavior>();
    private Vector2 interpTarget;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerCursorNetworkBehavior] OnNetworkSpawn for client {OwnerClientId}, IsLocal={IsLocalPlayer}");

        if (IsServer)
        {
            _allInstances.Add(this);

            if (_allInstances.Count == 1)
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        if (CursorManager.Instance == null)
            StartCoroutine(RetryCreateCursor());
        else if (spawnNetworkedCursor || IsLocalPlayer)
            CreateCursor();

        if (spawnNetworkedCursor && !IsLocalPlayer)
        {
            _normalizedCursorPos.OnValueChanged += (oldPos, newPos) =>
            {
                interpTarget = new Vector2(newPos.x * Screen.width, newPos.y * Screen.height);
            };
        }
    }

    private void OnClientConnected(ulong newClientId)
    {
        if (!IsServer || !spawnNetworkedCursor) return;
        StartCoroutine(DelayedSyncToNewClient(newClientId));
    }

    private IEnumerator DelayedSyncToNewClient(ulong newClientId)
    {
        yield return new WaitForSeconds(0.5f);
        foreach (var pc in _allInstances)
        {
            if (pc == null || pc.OwnerClientId == newClientId) continue;
            pc.SendCurrentPositionToClientRpc(pc._normalizedCursorPos.Value, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { newClientId } }
            });
        }
    }

    [ClientRpc]
    private void SendCurrentPositionToClientRpc(Vector2 pos, ClientRpcParams rpcParams = default)
    {
        if (cursorImage != null)
        {
            Vector2 screenPos = new Vector2(pos.x * Screen.width, pos.y * Screen.height);
            cursorImage.rectTransform.anchoredPosition = screenPos;
        }
    }

    public void SyncCursorPosition(Vector2 pos)
    {
        _normalizedCursorPos.Value = pos;
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
        if (CursorManager.Instance != null && (spawnNetworkedCursor || IsLocalPlayer))
            CreateCursor();
        else
        {
            Debug.LogError("CursorManager still missing after timeout.");
            enabled = false;
        }
    }

    private async void CreateCursor()
    {
        if (!IsLocalPlayer && !spawnNetworkedCursor) return; 

        if (!CursorManager.Instance.CheckCursorReady()) { enabled = false; return; }

        if (IsLocalPlayer) Cursor.visible = false;

        cursorTransform = CursorManager.Instance.SpawnCursor(spawnNetworkedCursor, out cursorImage);
        if (cursorImage == null) { enabled = false; return; }

        cursorTransform.pivot = new Vector2(0, 1);
        cursorTransform.anchorMin = Vector2.zero;
        cursorTransform.anchorMax = Vector2.zero;
        cursorTransform.anchoredPosition = Vector2.zero;

        // Use the color index assigned by server
        Color playerColor = await CursorManager.Instance.GetColorForPlayer(OwnerClientId);
        cursorImage.color = playerColor;
        Debug.Log($"Cursor created for player {OwnerClientId} with color {playerColor} (index {_colorIndex.Value})");
        
        cursorInput = cursorTransform.GetComponent<CursorInputHandler>();

        if (!IsLocalPlayer)
        {
            cursorTransform.GetComponent<VirtualMouseInput>().enabled = false;
            enabled = false;

            cursorInput.enabled = false;
        }

        if (IsLocalPlayer && spawnNetworkedCursor)
        {
            cursorInput.InitializeNetworkSync(this, spawnNetworkedCursor);
        }
    }

    private void Update()
    {
        if (cursorImage == null)
        {
            if (CursorManager.Instance != null && CursorManager.Instance.cursorEnabled)
                CreateCursor();
            return;
        }

        if (IsLocalPlayer)
        {
            cursorImage.rectTransform.anchoredPosition = Vector2.Lerp(cursorImage.rectTransform.anchoredPosition, interpTarget, Time.deltaTime * 10f);
        }
    }

    public void DisableCursor()
    {
        if (!IsLocalPlayer) return;

        Cursor.lockState = CursorLockMode.Locked;

        cursorImage.enabled = false;

        if (spawnNetworkedCursor)
            NotifyDisableCursorClientRpc();
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    public void NotifyDisableCursorClientRpc()
    {
        cursorImage.enabled = false;
    }

    public void EnableCursor()
    {
        if (!IsLocalPlayer) return;

        Cursor.lockState = CursorLockMode.Confined;

        var mouse = cursorImage.gameObject.GetComponentInParent<VirtualMouseInput>().virtualMouse;
        var mousePos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        InputState.Change(mouse.position, mousePos);

        cursorImage.rectTransform.anchoredPosition = mousePos;
        cursorImage.enabled = true;

        if (spawnNetworkedCursor)
            NotifyEnableCursorClientRpc();
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    public void NotifyEnableCursorClientRpc()
    {
        cursorImage.enabled = true;
    }

    public override void OnDestroy()
    { 
        _normalizedCursorPos.OnValueChanged = null;

        if (cursorImage != null) Destroy(cursorImage.gameObject);
        if (IsLocalPlayer && CursorManager.Instance != null && CursorManager.Instance.cursorEnabled)
            Cursor.visible = true;
    }
}