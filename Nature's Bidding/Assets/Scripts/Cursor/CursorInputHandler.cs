using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CursorInputHandler : MonoBehaviour
{
    public InputType CurrentInputType = InputType.StandardMouse;
    public enum InputType
    {
        StandardMouse,
        NonStandardCursor
    }

    bool _cursorPaused = false;
    bool networkedCursor = false;

    Image cursorImage;
    PlayerCursorNetworkBehavior cursorNetworkSync;

    void Awake()
    {
        cursorImage = GetComponentInChildren<Image>();

        InputSystem.onEvent += OnAnyInputEvent;
    }

    public void InitializeNetworkSync(PlayerCursorNetworkBehavior networkSync, bool isNetworked)
    {
        cursorNetworkSync = networkSync;
        networkedCursor = isNetworked;
    }

    public void OnDestroy()
    {
        InputSystem.onEvent -= OnAnyInputEvent;
    }

    private void OnAnyInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // We don't care about input events that are not state events
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

        InputType newType = device switch
        {
            Gamepad => InputType.NonStandardCursor,
            Mouse => InputType.StandardMouse,
            Keyboard => InputType.StandardMouse,
            _ => CurrentInputType // unknown device type, don't change anything
        };

        if (newType != CurrentInputType)
        {
            CurrentInputType = newType;
            Debug.Log($"[PlayerCursorNetworkBehavior] Switched to {CurrentInputType} (triggered by {device.displayName}).");
        }
    }

    private void Update()
    {

        if (Cursor.visible) Cursor.visible = false;

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            _cursorPaused = !_cursorPaused;
            Debug.Log($"Cursor pause: {(_cursorPaused ? "ON" : "OFF")}");
        }

        if (CursorUIManager.Instance == null || !CursorUIManager.Instance.cursorEnabled)
        {
            if (cursorImage != null) cursorImage.gameObject.SetActive(false);
            return;
        }

        Vector2 normPos;
        if (CurrentInputType == InputType.StandardMouse)
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if (!_cursorPaused)
            {
                if (cursorImage != null)
                    cursorImage.rectTransform.anchoredPosition = mousePos;
                normPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

                if (networkedCursor && cursorNetworkSync != null)
                    cursorNetworkSync.SyncCursorPosition(normPos);
            }
        }
        else if (CurrentInputType == InputType.NonStandardCursor && networkedCursor && cursorNetworkSync != null)
        {
            normPos = new Vector2(cursorImage.rectTransform.anchoredPosition.x / Screen.width, cursorImage.rectTransform.anchoredPosition.y / Screen.height);
            cursorNetworkSync.SyncCursorPosition(normPos);
        }

    }
}
