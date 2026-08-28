using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardTooltip : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text effectText;
    public TMP_Text descText;
    public TMP_Text costText;
    public TMP_Text stockText;

    [Tooltip("Gap between card and tooltip in screen pixels")]
    public float sideGap = 12f;

    [Tooltip("Assumed tooltip width in screen pixels for edge detection — match your prefab width x scaleFactor")]
    public float assumedWidthPx = 220f;

    private RectTransform _rt;
    private Canvas        _rootCanvas;

    void Awake()
    {
        _rt           = GetComponent<RectTransform>();
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.zero;
        _rt.pivot     = new Vector2(0f, 0.5f);
    }

    public void SetCanvas(Canvas canvas)
    {
        if (canvas == null) { GameLogger.Log(LogSeverity.Warning, "SetCanvas called with null!"); return; }

        // If already root just use it directly
        if (canvas.isRootCanvas) { _rootCanvas = canvas; return; }

        // Otherwise walk up to find the root
        Canvas c = canvas;
        while (c != null && !c.isRootCanvas)
        {
            Canvas parent = c.transform.parent?.GetComponentInParent<Canvas>();
            if (parent == null) break;   // c is as high as we can go, use it
            c = parent;
        }
        _rootCanvas = c;
        GameLogger.Log(LogSeverity.Verbose, $"SetCanvas — input:{canvas.name} resolved root:{_rootCanvas?.name} isRoot:{_rootCanvas?.isRootCanvas}");
    }

    public void Populate(ShopUpgrade upgrade, int owned)
    {
        if (nameText)   nameText.text   = upgrade.upgradeName;
        if (effectText) effectText.text = upgrade.FormattedEffect();
        if (descText)   descText.text   = upgrade.description;
        if (costText)   costText.text   = $"{upgrade.cost} coins";
        if (stockText)  stockText.text  = $"Owned: {owned} / {upgrade.maxPurchases}";
    }

    public void PopulatePot(int cost, bool used)
    {
        if (nameText)   nameText.text   = "Pot of Fate";
        if (effectText) effectText.text = "Random tarot reward";
        if (descText)   descText.text   = "Open the pot to draw 3 tarot cards and choose one reward.";
        if (costText)   costText.text   = $"{cost} coins";
        if (stockText)  stockText.text  = used ? "Already used this phase" : "Available";
    }

    public void PositionBesideCard(RectTransform cardRect, float extraOffsetX = 20f)
    {
        GameLogger.Log(LogSeverity.Debug, $"PositionBesideCard called — extraOffsetX={extraOffsetX}, rt:{_rt != null} card:{cardRect != null} canvas:{_rootCanvas?.name ?? "NULL"}");
        if (_rt == null || cardRect == null || _rootCanvas == null) return;

        float scale = _rootCanvas.scaleFactor;

        // Card corners in screen pixels (Screen Space Overlay: world = screen pixels)
        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);
        // 0=BL  1=TL  2=TR  3=BR
        float cardRightPx = corners[2].x;
        float cardLeftPx = corners[0].x;
        float cardCentrePxY = (corners[0].y + corners[1].y) * 0.5f;

        // Use assumed width for edge check since sizeDelta may be 0 with ContentSizeFitter
        bool goRight = (cardRightPx + sideGap + extraOffsetX + assumedWidthPx) <= Screen.width;

        // Screen pixel X of the tooltip's pivot point — extraOffsetX pushes further
        // away from the card in whichever direction the tooltip is opening, so it
        // clears a card that's visually larger than its RectTransform (e.g. popped
        // out of the basket) without overlapping.
        float screenX = goRight
            ? cardRightPx + sideGap + extraOffsetX
            : cardLeftPx - sideGap - extraOffsetX;
        float screenY = cardCentrePxY;   // vertically centred on the card

        // Convert screen pixels → canvas units (anchor at canvas bottom-left)
        float canvasX = screenX / scale;
        float canvasY = screenY / scale;

        _rt.pivot = new Vector2(goRight ? 0f : 1f, 0.5f);
        _rt.anchoredPosition = new Vector2(canvasX, canvasY);

        GameLogger.Log(LogSeverity.Verbose, $"cardRight:{cardRightPx:F0} cardLeft:{cardLeftPx:F0} centreY:{cardCentrePxY:F0} scale:{scale} goRight:{goRight} → canvas({canvasX:F0},{canvasY:F0}) screen({Screen.width}x{Screen.height})");
    }
}