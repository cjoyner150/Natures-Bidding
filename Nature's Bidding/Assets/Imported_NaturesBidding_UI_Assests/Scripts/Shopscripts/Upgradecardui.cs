using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Card UI Elements")]
    public Image    cardBackground;
    public Image    iconImage;
    public TMP_Text nameText;
    public TMP_Text effectText;
    public TMP_Text costText;
    public TMP_Text ownedText;
    public Button   cardButton;

    [Header("Selection Visuals")]
    public Image    shadowImage;        // Always-on darkened copy
    public Image    selectedImage;      // Toggles on/off when selected (will be an outline)

    [Header("Colors")]
    public Color normalColor   = new Color(0.15f, 0.15f, 0.2f, 1f);
    public Color selectedColor = new Color(0.2f,  0.4f,  0.7f, 1f);
    public Color maxedColor    = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Shadow Settings")]
    public float shadowScale = 1.05f;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("Selected Outline Settings")]   // NEW
    public float selectedScale = 1.15f;     // larger than shadow for outline effect
    public Color selectedOutlineColor = Color.yellow;  // or gold: new Color(1f, 0.8f, 0f, 1f)

    private int    _ownedCount;
    private Action _onClick;
    private Action _onHover;
    private Action _onHoverExit;

    void Awake()
    {
        if (cardButton == null)
            cardButton = GetComponent<Button>();

        if (shadowImage != null)
            shadowImage.raycastTarget = false;
        if (selectedImage != null)
        {
            selectedImage.raycastTarget = false;
            selectedImage.gameObject.SetActive(false);
        }
    }

    public ShopUpgrade Upgrade { get; set; }

    #region Populate

    public void Populate(ShopUpgrade upgrade, int ownedCount, Action onClick, Action onHover = null, Action onHoverExit = null)
    {
        Upgrade      = upgrade;
        _ownedCount  = ownedCount;
        _onClick     = onClick;
        _onHover     = onHover;
        _onHoverExit = onHoverExit;
        SetSelected(false);

        if (nameText)   nameText.text   = upgrade.upgradeName;
        if (effectText) effectText.text = upgrade.FormattedEffect();
        if (costText)   costText.text   = $"{upgrade.cost}";
        if (iconImage && upgrade.icon)
        {
            iconImage.sprite = upgrade.icon;
            SetupVisuals();   // creates both shadow and selected images from the icon
        }

        UpdateOwnedCount(ownedCount);
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => _onClick?.Invoke());
        }

        if (cardBackground) cardBackground.color = upgrade.cardColor;
    }

    public void SetPotCard(string title, string desc, string cost, bool used, Action onClick, Action onHover = null, Action onHoverExit = null)
    {
        Upgrade      = null;
        _onClick     = onClick;
        _onHover     = onHover;
        _onHoverExit = onHoverExit;
        _ownedCount  = 0;

        if (nameText)   nameText.text   = title;
        if (effectText) effectText.text = desc;
        if (costText)   costText.text   = cost;
        if (ownedText)  ownedText.text  = used ? "Used" : "Available";

        if (iconImage != null && iconImage.sprite != null)
            SetupVisuals();

        if (cardBackground)
            cardBackground.color = used
                ? maxedColor
                : new Color(0.25f, 0.18f, 0.08f, 1f);

        if (cardButton != null)
        {
            cardButton.interactable = !used;
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => _onClick?.Invoke());
        }

        SetSelected(false);
    }

    #endregion

    #region Visuals Setup (Shadow + Selected Outline)

    private void SetupVisuals()
    {
        if (iconImage == null || iconImage.sprite == null) return;

        // Setup shadow (always-on)
        if (shadowImage != null)
        {
            shadowImage.sprite = iconImage.sprite;
            shadowImage.color = shadowColor;
            CopyRectTransform(iconImage, shadowImage);
            shadowImage.transform.localScale = new Vector3(shadowScale, shadowScale, 1f);
            EnsureBehind(iconImage, shadowImage);
            shadowImage.gameObject.SetActive(true);
        }

        // Setup selected outline (starts invisible)
        if (selectedImage != null)
        {
            selectedImage.sprite = iconImage.sprite;
            selectedImage.color = selectedOutlineColor;
            CopyRectTransform(iconImage, selectedImage);
            selectedImage.transform.localScale = new Vector3(selectedScale, selectedScale, 1f);
            EnsureBehind(iconImage, selectedImage);
            selectedImage.gameObject.SetActive(false);
        }
    }

    private void CopyRectTransform(Image source, Image target)
    {
        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (sourceRect != null && targetRect != null)
        {
            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.offsetMin = sourceRect.offsetMin;
            targetRect.offsetMax = sourceRect.offsetMax;
            targetRect.pivot = sourceRect.pivot;
        }
    }

    private void EnsureBehind(Image front, Image back)
    {
        int frontSibling = front.transform.GetSiblingIndex();
        int backSibling = back.transform.GetSiblingIndex();
        if (backSibling > frontSibling)
            back.transform.SetSiblingIndex(frontSibling);
    }

    #endregion

    #region Selection

    public void SetSelected(bool selected)
    {
        // Toggle the selected outline image
        if (selectedImage != null)
            selectedImage.gameObject.SetActive(selected);
            
        if (shadowImage != null)
            shadowImage.gameObject.SetActive(!selected);  // Optionally hide shadow when selected for clearer outline

        // Optional background color change
        if (cardBackground && Upgrade != null)
        {
            bool maxed = _ownedCount >= Upgrade.maxPurchases;
            if (!maxed)
                cardBackground.color = selected ? selectedColor : Upgrade.cardColor;
        }
    }

    #endregion

    #region State Updates

    public void UpdateOwnedCount(int newCount)
    {
        if (Upgrade == null) return;
        _ownedCount = newCount;
        bool maxed = _ownedCount >= Upgrade.maxPurchases;

        if (ownedText)
            ownedText.text = $"Owned: {_ownedCount}/{Upgrade.maxPurchases}";

        if (cardBackground)
            cardBackground.color = maxed ? maxedColor : (Upgrade?.cardColor ?? normalColor);

        if (cardButton)
            cardButton.interactable = !maxed;
    }

    #endregion

    #region Hover

    public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke();
    public void OnPointerExit(PointerEventData eventData)  => _onHoverExit?.Invoke();

    #endregion
}