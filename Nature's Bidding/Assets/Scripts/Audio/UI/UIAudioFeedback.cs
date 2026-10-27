using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIAudioFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler,
    ISelectHandler,
    ISubmitHandler
{
    [SerializeField] private bool playHover = true;
    [SerializeField] private bool playClick = true;

    private Selectable selectable;
    private Button button;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        button = selectable as Button;

        if (button != null)
            button.onClick.AddListener(PlayButtonClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(PlayButtonClick);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        PlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button == null && eventData.button == PointerEventData.InputButton.Left)
            PlayClick();
    }

    public void OnSelect(BaseEventData _)
    {
        PlayHover();
    }

    public void OnSubmit(BaseEventData _)
    {
        if (button == null)
            PlayClick();
    }

    private void PlayHover()
    {
        if (!playHover || !CanPlay())
            return;

        GameAudioController.Instance?.PlayUIHover();
    }

    private void PlayClick()
    {
        if (!playClick || !CanPlay())
            return;

        GameAudioController.Instance?.PlayUIClick();
    }

    private void PlayButtonClick()
    {
        // Button validates active/interactable state before invoking onClick.
        // The callback must still run if an earlier listener deactivates the hierarchy.
        if (!playClick || !enabled)
            return;

        GameAudioController.Instance?.PlayUIClick();
    }

    private bool CanPlay()
    {
        return isActiveAndEnabled &&
               (selectable == null || selectable.IsInteractable());
    }
}
