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

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData _)
    {
        PlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            PlayClick();
    }

    public void OnSelect(BaseEventData _)
    {
        PlayHover();
    }

    public void OnSubmit(BaseEventData _)
    {
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

    private bool CanPlay()
    {
        return isActiveAndEnabled &&
               (selectable == null || selectable.IsInteractable());
    }
}
