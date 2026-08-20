using UnityEngine;
using UnityEngine.InputSystem;

public class MapCameraController : MonoBehaviour
{
    [Header("Scrolling Settings")]
    [Tooltip("Keep this low. The new Input System scroll wheel outputs much larger numbers (like 120) than the old system.")]
    public float scrollSpeed = 0.5f; 
    public float dragSpeed = 15f;

    [Header("Map Boundaries")]
    public float minY = -2f;
    public float maxY = 30f; 

    private Camera cam;
    private Vector2 dragOrigin;
    public MapSettingsSO mapSettings;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        
        if (Mouse.current == null) return;

        HandleMouseDrag();
        HandleScrollWheel();
        ClampCameraPosition();
    }

    private void HandleMouseDrag()
    {
        // Check if Right Click or Middle Click was pressed THIS FRAME
        bool rightPressed = Mouse.current.rightButton.wasPressedThisFrame;
        bool middlePressed = Mouse.current.middleButton.wasPressedThisFrame;

        if (rightPressed || middlePressed)
        {
            dragOrigin = Mouse.current.position.ReadValue();
            return;
        }

        // Check if Right Click or Middle Click is CURRENTLY HELD DOWN
        bool rightHeld = Mouse.current.rightButton.isPressed;
        bool middleHeld = Mouse.current.middleButton.isPressed;

        if (rightHeld || middleHeld)
        {
            Vector2 currentMousePos = Mouse.current.position.ReadValue();
            Vector3 difference = cam.ScreenToViewportPoint(currentMousePos - dragOrigin);
            
            // Move up/down based on vertical drag
            Vector3 move = Vector3.zero;
            if (mapSettings != null && mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop)
                move = new Vector3(0, -difference.y * dragSpeed, 0); // Pan Vertical
            else
                move = new Vector3(-difference.x * dragSpeed, 0, 0);
            
            transform.Translate(move, Space.World);
            
            dragOrigin = currentMousePos;
        }
    }

    private void HandleScrollWheel()
    {
        // The new Input System returns a Vector2 for scroll. We only care about Y 
        float scroll = Mouse.current.scroll.ReadValue().y;
        
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (mapSettings != null && mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop)
                transform.Translate(Vector3.up * scroll * scrollSpeed * Time.deltaTime, Space.World);
            else
                transform.Translate(Vector3.right * scroll * scrollSpeed * Time.deltaTime, Space.World);
        }
    }

    private void ClampCameraPosition()
    {
        Vector3 clampedPos = transform.position;

        if (mapSettings != null && mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop)
        {
            clampedPos.y = Mathf.Clamp(clampedPos.y, minY, maxY);
            clampedPos.x = 0f;
        }
        else // LeftToRight
        {
            clampedPos.x = Mathf.Clamp(clampedPos.x, minY, maxY);
            clampedPos.y = 0f;
        }
        
        transform.position = clampedPos;
    }
}