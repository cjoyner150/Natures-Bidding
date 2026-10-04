using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MapCameraController : MonoBehaviour
{
    [Header("Scrolling Settings")]
    [Tooltip("Keep this low. The new Input System scroll wheel outputs much larger numbers (like 120) than the old system.")]
    public float scrollSpeed = 0.5f;
    public float dragSpeed = 15f;

    [Header("Gamepad Scrolling")]
    [Tooltip("World units per second at full stick deflection.")]
    public float gamepadScrollSpeed = 12f;
    [Range(0f, 0.9f)] public float gamepadDeadzone = 0.2f;
    public bool invertGamepadScroll = false;

    [Header("Map Boundaries")]
    public float minY = -2f;
    public float maxY = 30f;

    private Camera cam;
    private Vector2 dragOrigin;
    private Coroutine focusCoroutine;
    private bool isFocusing;
    public MapSettingsSO mapSettings;

    private bool IsBottomToTop =>
        mapSettings != null && mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (isFocusing) return;

        if (Mouse.current != null)
        {
            HandleMouseDrag();
            HandleScrollWheel();
        }

        if (Gamepad.current != null)
            HandleGamepadScroll();

        ClampCameraPosition();
    }

    private void HandleMouseDrag()
    {
        bool rightPressed = Mouse.current.rightButton.wasPressedThisFrame;
        bool middlePressed = Mouse.current.middleButton.wasPressedThisFrame;

        if (rightPressed || middlePressed)
        {
            dragOrigin = Mouse.current.position.ReadValue();
            return;
        }

        bool rightHeld = Mouse.current.rightButton.isPressed;
        bool middleHeld = Mouse.current.middleButton.isPressed;

        if (rightHeld || middleHeld)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            Vector3 difference = cam.ScreenToViewportPoint(currentMousePos - dragOrigin);

            Vector3 move = IsBottomToTop
                ? new Vector3(0, -difference.y * dragSpeed, 0)
                : new Vector3(-difference.x * dragSpeed, 0, 0);

            transform.Translate(move, Space.World);
            dragOrigin = currentMousePos;
        }
    }

    private void HandleScrollWheel()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) <= 0.01f) return;

        Vector3 axis = IsBottomToTop ? Vector3.up : Vector3.right;
        transform.Translate(axis * scroll * scrollSpeed * Time.deltaTime, Space.World);
    }

    private void HandleGamepadScroll()
    {
        Vector2 stick = Gamepad.current.rightStick.ReadValue();

        // Use the stick axis that matches the map's scroll direction.
        float input = IsBottomToTop ? stick.y : stick.x;
        if (Mathf.Abs(input) < gamepadDeadzone) return;

        // Rescale so motion starts at zero just past the deadzone instead of jumping.
        float scaled = Mathf.Sign(input) * Mathf.InverseLerp(gamepadDeadzone, 1f, Mathf.Abs(input));
        if (invertGamepadScroll) scaled = -scaled;

        Vector3 axis = IsBottomToTop ? Vector3.up : Vector3.right;
        transform.Translate(axis * scaled * gamepadScrollSpeed * Time.unscaledDeltaTime, Space.World);
    }

    private void ClampCameraPosition()
    {
        Vector3 clampedPos = transform.position;

        if (IsBottomToTop)
        {
            clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
            clampedPos.x = 0f;
        }
        else
        {
            clampedPos.x = Mathf.Clamp(clampedPos.x, minY, maxY);
            clampedPos.y = 0f;
        }

        transform.position = clampedPos;
    }

    public bool TryScreenToMapPosition(Vector2 screenPosition, out Vector2 mapPosition)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane mapPlane = new Plane(Vector3.forward, Vector3.zero);
        if (mapPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPosition = ray.GetPoint(distance);
            mapPosition = new Vector2(worldPosition.x, worldPosition.y);
            return true;
        }

        mapPosition = Vector2.zero;
        return false;
    }

    public Vector2 MapToScreenPosition(Vector2 mapPosition)
    {
        Vector3 screenPosition = cam.WorldToScreenPoint(new Vector3(mapPosition.x, mapPosition.y, 0f));
        return new Vector2(screenPosition.x, screenPosition.y);
    }

    public void FocusOnMapPosition(Vector2 mapPosition, float duration)
    {
        if (focusCoroutine != null)
            StopCoroutine(focusCoroutine);

        focusCoroutine = StartCoroutine(FocusOnMapPositionRoutine(mapPosition, duration));
    }

    private IEnumerator FocusOnMapPositionRoutine(Vector2 mapPosition, float duration)
    {
        isFocusing = true;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition;

        if (IsBottomToTop)
        {
            targetPosition.x = 0f;
            targetPosition.y = Mathf.Clamp(mapPosition.y, minY, maxY);
        }
        else
        {
            targetPosition.x = Mathf.Clamp(mapPosition.x, minY, maxY);
            targetPosition.y = 0f;
        }

        if (duration <= 0f)
        {
            transform.position = targetPosition;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            transform.position = targetPosition;
        }

        isFocusing = false;
        focusCoroutine = null;
    }
}
