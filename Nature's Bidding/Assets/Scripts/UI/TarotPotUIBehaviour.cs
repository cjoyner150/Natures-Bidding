using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TarotPotUIBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private PotType potType;
    public PotType PotType => potType;

    [SerializeField] private HorizontalLayoutGroup layoutGroup;
    public HorizontalLayoutGroup LayoutGroup => layoutGroup;

    public void OnPointerClick(PointerEventData eventData)
    {
        TarotPotManager.OnPotUIClickedEvent?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TarotPotManager.OnPotUIHoveredEvent?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //tarotManager.OnPotExited();
    }

}
