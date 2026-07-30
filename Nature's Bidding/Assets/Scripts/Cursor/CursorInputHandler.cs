using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorInputHandler : MonoBehaviour
{
    bool _cursorPaused = false;
    bool networkedCursor = false;

    Image cursorImage;
    PlayerCursorNetworkBehavior cursorNetworkSync;

    void Awake()
    {
        cursorImage = GetComponentInChildren<Image>();
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
            Debug.Log($"Cursor pause: {(_cursorPaused ? "ON" : "OFF")}");
        }

        if (CursorUIManager.Instance == null || !CursorUIManager.Instance.cursorEnabled)
        {
            if (cursorImage != null) cursorImage.gameObject.SetActive(false);
            return;
        }

        Vector2 normPos;
        if (InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.MouseAndKeyboard)
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
        else if (InputDeviceTracker.CurrentInputType == InputDeviceTracker.InputType.Gamepad && networkedCursor && cursorNetworkSync != null)
        {
            normPos = new Vector2(cursorImage.rectTransform.anchoredPosition.x / Screen.width, cursorImage.rectTransform.anchoredPosition.y / Screen.height);
            cursorNetworkSync.SyncCursorPosition(normPos);
        }
    }
}