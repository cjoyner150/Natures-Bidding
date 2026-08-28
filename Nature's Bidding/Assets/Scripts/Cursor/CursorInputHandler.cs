using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CursorInputHandler : MonoBehaviour
{
    bool _cursorPaused = false;
    bool networkedCursor = false;

    Image cursorImage;
    RectTransform cursorRoot;
    PlayerCursorNetworkBehavior cursorNetworkSync;
    VirtualMouseInput virtualMouseInput;

    private InputDeviceTracker.InputType _lastInputType;

    void Awake()
    {
        cursorImage = GetComponentInChildren<Image>();
        virtualMouseInput = GetComponent<VirtualMouseInput>();
        cursorRoot = virtualMouseInput.cursorTransform;
        _lastInputType = InputDeviceTracker.CurrentInputType;
        GameLogger.Log(LogSeverity.Verbose, $"Awake. cursorRoot={cursorRoot}, cursorImage={cursorImage}, virtualMouseInput.cursorTransform={virtualMouseInput.cursorTransform}");
    }

    public void InitializeNetworkSync(PlayerCursorNetworkBehavior networkSync, bool isNetworked)
    {
        cursorNetworkSync = networkSync;
        networkedCursor = isNetworked;
    }

    private void Update()
    {
        if (Cursor.visible) Cursor.visible = false;

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            _cursorPaused = !_cursorPaused;
        }

        if (CursorUIManager.Instance == null || !CursorUIManager.Instance.cursorEnabled)
        {
            if (cursorImage != null) cursorImage.gameObject.SetActive(false);
            return;
        }

        // Detect input type change: sync position and enable/disable the
        // stick action so VirtualMouseInput's own UpdateMotion doesn't fight
        // mouse-driven position updates.
        if (InputDeviceTracker.CurrentInputType != _lastInputType)
        {
            GameLogger.Log(LogSeverity.Info, $"Switch detected: {_lastInputType} -> {InputDeviceTracker.CurrentInputType}");

            if (InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad)
            {
                if (virtualMouseInput != null && virtualMouseInput.virtualMouse != null && cursorRoot != null)
                {
                    InputState.Change(virtualMouseInput.virtualMouse.position, cursorRoot.anchoredPosition);
                }
                virtualMouseInput.stickAction.action?.Enable();
                GameLogger.Log(LogSeverity.Verbose, $"stickAction enabled: {virtualMouseInput.stickAction.action?.enabled}");
            }
            else
            {
                virtualMouseInput.stickAction.action?.Disable();
                GameLogger.Log(LogSeverity.Verbose, $"stickAction disabled: {virtualMouseInput.stickAction.action?.enabled}");
            }

            _lastInputType = InputDeviceTracker.CurrentInputType;
        }

        Vector2 normPos;
        if (InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard)
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if (!_cursorPaused)
            {
                if (cursorRoot != null)
                    cursorRoot.anchoredPosition = mousePos;

                normPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

                if (networkedCursor && cursorNetworkSync != null)
                    cursorNetworkSync.SyncCursorPosition(normPos);
            }
        }
        else if (InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad && networkedCursor && cursorNetworkSync != null)
        {
            normPos = new Vector2(cursorRoot.anchoredPosition.x / Screen.width, cursorRoot.anchoredPosition.y / Screen.height);
            cursorNetworkSync.SyncCursorPosition(normPos);
        }
    }
}