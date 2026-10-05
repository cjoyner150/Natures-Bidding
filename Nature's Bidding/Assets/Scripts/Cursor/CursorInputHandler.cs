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

        bool gamepadNow = InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad;
        var stick = virtualMouseInput != null ? virtualMouseInput.stickAction.action : null;

        if (stick != null && stick.enabled != gamepadNow)
        {
            if (gamepadNow)
            {
                // Entering gamepad control: continue from where the cursor visually is,
                // so VirtualMouseInput doesn't resume from a stale internal position.
                if (virtualMouseInput.virtualMouse != null && cursorRoot != null)
                    InputState.Change(virtualMouseInput.virtualMouse.position, cursorRoot.anchoredPosition);

                stick.Enable();
                GameLogger.Log(LogSeverity.Verbose, "stickAction enabled (gamepad)");
            }
            else
            {
                stick.Disable();
                GameLogger.Log(LogSeverity.Verbose, "stickAction disabled (mouse/keyboard)");
            }
        }

        _lastInputType = InputDeviceTracker.CurrentInputType;

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