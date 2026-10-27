using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TarotPotUIBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] TextMeshProUGUI PotPriceTagTMP;
    [SerializeField] private PotTypeSO potType;
    public PotTypeSO PotType => potType;

    [SerializeField] private HorizontalLayoutGroup layoutGroup;
    public HorizontalLayoutGroup LayoutGroup => layoutGroup;

    void Awake()
    {
        if (PotPriceTagTMP == null)
        {
            GameLogger.Log(LogSeverity.Error, "Pot price tmp is null. Set the price tag tmp in the inspector.");
            return;
        }

        if (potType == null)
        {
            GameLogger.Log(LogSeverity.Error, "Pot type is null. Set the pot type in the inspector.");
            return;
        }

        PotPriceTagTMP.text = potType.cost.ToString();
    }

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
        TarotPotManager.OnPotUIExitedEvent?.Invoke(this);
    }

}
