using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

[RequireComponent(typeof(VirtualMouseInput))]
public class VirtualMouseLimiter : MonoBehaviour
{
    VirtualMouseInput virtualMouseInput;
    RectTransform canvasRect;

    private void Awake()
    {
        virtualMouseInput = GetComponent<VirtualMouseInput>();
        canvasRect = UtilityExtensions.GetInParents<Canvas>(gameObject).gameObject.GetComponent<RectTransform>();
    }

    private void Update()
    {
        transform.localScale = Vector3.one * (1 / canvasRect.localScale.x);
        transform.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (virtualMouseInput == null || !virtualMouseInput.enabled) return;

        Vector2 before = virtualMouseInput.virtualMouse.position.value;
        Vector2 mousePos = before;
        mousePos.x = Mathf.Clamp(mousePos.x, 50, Screen.width - 50);
        mousePos.y = Mathf.Clamp(mousePos.y, 50, Screen.height - 50);

        InputState.Change(virtualMouseInput.virtualMouse.position, mousePos);

    }
}
