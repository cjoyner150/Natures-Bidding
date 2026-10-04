using UnityEngine;
/// <summary>
/// Drives map node hover/click from MapPointer (hardware mouse OR gamepad virtual cursor),
/// bypassing the EventSystem so both input types hit the same world-space colliders.
/// </summary>
public class MapNodeSelector : MonoBehaviour
{
    [SerializeField] private MapCameraController cameraController;
    [SerializeField] private LayerMask nodeLayers = ~0;

    private NodeVisual _hovered;

    private void Awake()
    {
        if (cameraController == null) cameraController = FindFirstObjectByType<MapCameraController>();
    }

    private void Update()
    {
        if (cameraController == null) return;

        NodeVisual underCursor = null;
        if (cameraController.TryScreenToMapPosition(MapPointer.ScreenPosition, out Vector2 mapPos))
        {
            Collider2D hit = Physics2D.OverlapPoint(mapPos, nodeLayers);
            if (hit != null) underCursor = hit.GetComponentInParent<NodeVisual>();
        }

        if (underCursor != _hovered)
        {
            if (_hovered != null) _hovered.HoverExit();
            if (underCursor != null) underCursor.HoverEnter();
            _hovered = underCursor;
        }

        if (_hovered != null && MapPointer.SelectPressedThisFrame)
            _hovered.Select();
    }

    private void OnDisable()
    {
        if (_hovered != null) { _hovered.HoverExit(); _hovered = null; }
    }
}