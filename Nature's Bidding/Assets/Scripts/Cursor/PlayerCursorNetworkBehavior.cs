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
    [SerializeField] private bool syncCursorPosition = true;

    private NetworkVariable<Vector2> _normalizedCursorPos = new NetworkVariable<Vector2>(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<int> _colorIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private RectTransform cursorTransform;
    private CursorInputHandler cursorInput;
    private VirtualMouseInput virtualMouseInput;
    private Image cursorImage;

    private static List<PlayerCursorNetworkBehavior> _allInstances = new List<PlayerCursorNetworkBehavior>();
    private Vector2 interpTarget;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerCursorNetworkBehavior] OnNetworkSpawn for client {OwnerClientId}, IsLocal={IsOwner}");

        if (IsServer)
        {
            _allInstances.Add(this);

            if (_allInstances.Count == 1)
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        if (syncCursorPosition && !IsOwner)
        {
            _normalizedCursorPos.OnValueChanged += (oldPos, newPos) =>
            {
                interpTarget = new Vector2(newPos.x * Screen.width, newPos.y * Screen.height);
            };
        }

        if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat || 
            PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Lobby)
        {
            PlayerPauseManager.OnPaused += EnableCursor;
            PlayerPauseManager.OnResumed += DisableCursor;
        }

        if (syncCursorPosition || IsOwner) CreateCursor();
    }

    private void OnClientConnected(ulong newClientId)
    {
        if (!IsServer || !syncCursorPosition) return;
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

    private async void CreateCursor()
    {
        if (!IsOwner && !syncCursorPosition) return; 
        Debug.Log($"[PlayerCursorNetworkBehavior] CreateCursor START. OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.Singleton.LocalClientId}, IsOwner={IsOwner}");
        await UniTask.WaitUntil(() => CursorUIManager.Instance != null);

        if (!CursorUIManager.Instance.CheckCursorReady()) { enabled = false; return; }

        if (IsOwner) Cursor.visible = false;

        cursorTransform = CursorUIManager.Instance.SpawnCursor(syncCursorPosition, out cursorImage);
        if (cursorImage == null) { enabled = false; return; }

        cursorTransform.pivot = new Vector2(0, 1);
        cursorTransform.anchorMin = Vector2.zero;
        cursorTransform.anchorMax = Vector2.zero;
        cursorTransform.anchoredPosition = Vector2.zero;

        // Use the color index assigned by server
        Color playerColor = await CursorUIManager.Instance.GetColorForPlayer(OwnerClientId);
        cursorImage.color = playerColor;
        Debug.Log($"Cursor created for player {OwnerClientId} with color {playerColor} (index {_colorIndex.Value})");
        
        cursorInput = cursorTransform.GetComponent<CursorInputHandler>();
        virtualMouseInput = cursorTransform.GetComponent<VirtualMouseInput>();

        if (!IsOwner)
        {
            virtualMouseInput.enabled = false;
            cursorInput.enabled = false;
        }

        if (IsOwner && syncCursorPosition)
        {
            cursorInput.InitializeNetworkSync(this, syncCursorPosition);
        }

        if (IsOwner)
        {
            GivePrivateActionCopies();

            var state = PersistentGameStateManager.Instance.State;
            Debug.Log($"[PlayerCursorNetworkBehavior] CreateCursor: IsOwner=true, GameState={state}");

            if (state == PersistentGameStateManager.GameState.Combat ||
                state == PersistentGameStateManager.GameState.Lobby)
            {
                Debug.Log("[PlayerCursorNetworkBehavior] Calling DisableCursor (state is Combat/Lobby)");
                DisableCursor();
            }
            else
            {
                Debug.Log("[PlayerCursorNetworkBehavior] Calling EnableCursor (state is not Combat/Lobby)");
                EnableCursor();
            }
        }
    }

    private void GivePrivateActionCopies()
    {
        virtualMouseInput.stickAction = CloneActionProperty(virtualMouseInput.stickAction);
        virtualMouseInput.leftButtonAction = CloneActionProperty(virtualMouseInput.leftButtonAction);
        virtualMouseInput.rightButtonAction = CloneActionProperty(virtualMouseInput.rightButtonAction);
        virtualMouseInput.middleButtonAction = CloneActionProperty(virtualMouseInput.middleButtonAction);
        virtualMouseInput.forwardButtonAction = CloneActionProperty(virtualMouseInput.forwardButtonAction);
        virtualMouseInput.backButtonAction = CloneActionProperty(virtualMouseInput.backButtonAction);
    }

    private void DisableInputActions()
    {
        virtualMouseInput.stickAction.action.Disable();
        virtualMouseInput.leftButtonAction.action.Disable();
        virtualMouseInput.rightButtonAction.action.Disable();
        virtualMouseInput.middleButtonAction.action.Disable();
        virtualMouseInput.forwardButtonAction.action.Disable();
        virtualMouseInput.backButtonAction.action.Disable();
    }

    private void EnableInputActions()
    {
        virtualMouseInput.stickAction.action.Enable();
        virtualMouseInput.leftButtonAction.action.Enable();
        virtualMouseInput.rightButtonAction.action.Enable();
        virtualMouseInput.middleButtonAction.action.Enable();
        virtualMouseInput.forwardButtonAction.action.Enable();
        virtualMouseInput.backButtonAction.action.Enable();
    }

    private InputActionProperty CloneActionProperty(InputActionProperty original)
    {
        var sourceAction = original.action;
        if (sourceAction == null) return original;

        var clonedAction = sourceAction.Clone();
        clonedAction.Enable();

        return new InputActionProperty(clonedAction);
    }

    private void Update()
    {
        if (!IsOwner && syncCursorPosition)
        {
            if (cursorImage.IsDestroyed() || cursorImage == null) return;
            cursorImage.rectTransform.anchoredPosition = Vector2.Lerp(cursorImage.rectTransform.anchoredPosition, interpTarget, Time.deltaTime * 10f);
        }
    }

    public void DisableCursor()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Locked;

        cursorImage.enabled = false;

        DisableInputActions();

        if (syncCursorPosition)
            NotifyDisableCursorClientRpc();
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    public void NotifyDisableCursorClientRpc()
    {
        cursorImage.enabled = false;
    }

    public void EnableCursor()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Confined;

        var mouse = cursorImage.gameObject.GetComponentInParent<VirtualMouseInput>().virtualMouse;
        var mousePos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        InputState.Change(mouse.position, mousePos);

        EnableInputActions();

        cursorImage.rectTransform.anchoredPosition = mousePos;
        cursorImage.enabled = true;

        if (syncCursorPosition)
            NotifyEnableCursorClientRpc();
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    public void NotifyEnableCursorClientRpc()
    {
        cursorImage.enabled = true;
    }

    public bool IsCursorReadyAndEnabled()
    {
        if (CursorUIManager.Instance == null) return false;
        if (cursorImage == null) return false;
        if (!cursorImage.enabled) return false;

        return true;
    }

    public override void OnDestroy()
    { 
        PlayerPauseManager.OnPaused -= EnableCursor;
        PlayerPauseManager.OnResumed -= DisableCursor;

        _normalizedCursorPos.OnValueChanged = null;

        if (cursorImage != null) Destroy(cursorImage.gameObject);
        if (IsOwner && CursorUIManager.Instance != null && CursorUIManager.Instance.cursorEnabled)
            Cursor.visible = true;
    }
}