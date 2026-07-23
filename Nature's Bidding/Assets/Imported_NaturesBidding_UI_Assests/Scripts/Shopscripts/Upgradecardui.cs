using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using UnityEngine.Serialization;

public class UpgradeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Card UI Elements")]
    public Image    cardBackground;
    public Image    iconImage;
    public Image    costIconImage;
    public TMP_Text nameText;
    public TMP_Text effectText;
    public TMP_Text costText;
    public TMP_Text ownedText;
    public Button   cardButton;

    [Header("Selection Visuals")]
    [FormerlySerializedAs("shadowImage")]
    public Image    outlineImage;       // Always-on outline copy
    public Image    selectedImage;      // Toggles on/off when selected (stronger outline)

    [Header("Colors")]
    public Color normalColor   = new Color(0.15f, 0.15f, 0.2f, 1f);
    public Color selectedColor = new Color(0.2f,  0.4f,  0.7f, 1f);
    public Color maxedColor    = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Outline Settings")]
    public float outlineScale = 1.08f;
    public Color outlineColor = new Color(1f, 0.8f, 0f, 1f);

    [Header("Selected Outline Settings")]   // NEW
    public float selectedScale = 1.15f;     // larger than shadow for outline effect
    public Color selectedOutlineColor = Color.yellow;  // or gold: new Color(1f, 0.8f, 0f, 1f)

    [Header("Sold State")]
    public Sprite soldIconSprite;
    public Sprite soldCostIconSprite;
    public string soldLabelText = "SOLD";
    public float soldLiftAmount = 16f;
    public float soldLiftDuration = 0.18f;
    public float soldDisableDelay = 0.08f;

    private int    _ownedCount;
    private Action _onClick;
    private Action _onHover;
    private Action _onHoverExit;
    private bool   _lockedOut;
    private Sprite _originalIconSprite;
    private Sprite _originalCostIconSprite;
    private RectTransform _iconRect;
    private RectTransform _costIconRect;
    private Vector2 _iconStartPosition;
    private Vector2 _costIconStartPosition;
    private Coroutine _soldRoutine;
    private int _lastClickFrame = -1;

    void Awake()
    {
        if (cardButton == null)
            cardButton = GetComponent<Button>();
        if (cardButton == null)
            cardButton = GetComponentInChildren<Button>(true);

        if (outlineImage != null)
            outlineImage.raycastTarget = false;
        if (selectedImage != null)
        {
            selectedImage.raycastTarget = false;
            selectedImage.gameObject.SetActive(false);
        }

        if (iconImage != null)
        {
            _originalIconSprite = iconImage.sprite;
            _iconRect = iconImage.rectTransform;
            _iconStartPosition = _iconRect.anchoredPosition;
        }
        if (costIconImage != null)
        {
            _originalCostIconSprite = costIconImage.sprite;
            _costIconRect = costIconImage.rectTransform;
            _costIconStartPosition = _costIconRect.anchoredPosition;
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
        _lockedOut   = false;
        SetSelected(false);

        if (nameText)   nameText.text   = upgrade.upgradeName;
        if (effectText) effectText.text = upgrade.FormattedEffect();
        if (costText)   costText.text   = $"{upgrade.cost}";
        if (iconImage && upgrade.icon)
        {
            iconImage.sprite = upgrade.icon;
            _originalIconSprite = upgrade.icon;
            SetupVisuals();   // creates both outline layers from the icon
        }

        UpdateOwnedCount(ownedCount);
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(HandleCardClicked);
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
        _lockedOut   = used;

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
            cardButton.onClick.AddListener(HandleCardClicked);
        }

        SetSelected(false);
    }

    public void SetLockedOut(string statusText = "Bought")
    {
        _lockedOut = true;

        if (_soldRoutine != null)
            StopCoroutine(_soldRoutine);

        if (cardButton != null)
            cardButton.interactable = false;

        if (ownedText != null)
            ownedText.text = statusText;

        if (costText != null)
            costText.text = soldLabelText;

        if (iconImage != null)
        {
            if (soldIconSprite != null)
                iconImage.sprite = soldIconSprite;
            else if (_originalIconSprite != null)
                iconImage.sprite = _originalIconSprite;

            iconImage.color = new Color(1f, 1f, 1f, 0.75f);
        }

        if (selectedImage != null)
        {
            selectedImage.sprite = Upgrade != null && Upgrade.shadowSprite != null
                ? Upgrade.shadowSprite
                : _originalIconSprite;
            selectedImage.color = new Color(1f, 1f, 1f, 0.55f);
            selectedImage.gameObject.SetActive(true);
        }

        if (costIconImage != null && soldCostIconSprite != null)
            costIconImage.sprite = soldCostIconSprite;

        if (cardBackground != null)
            cardBackground.color = maxedColor;

        if (outlineImage != null)
            outlineImage.gameObject.SetActive(false);

        if (selectedImage != null)
            selectedImage.gameObject.SetActive(false);

        _soldRoutine = StartCoroutine(PlaySoldTransition());
    }

    IEnumerator PlaySoldTransition()
    {
        if (iconImage != null && _iconRect != null)
        {
            Vector2 start = _iconStartPosition;
            Vector2 lift  = start + Vector2.up * Mathf.Abs(soldLiftAmount);
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, soldLiftDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                _iconRect.anchoredPosition = Vector2.LerpUnclamped(start, lift, easedT);
                yield return null;
            }

            _iconRect.anchoredPosition = lift;
        }

        if (costIconImage != null && _costIconRect != null && soldDisableDelay > 0f)
            yield return new WaitForSecondsRealtime(soldDisableDelay);

        if (iconImage != null)
            iconImage.gameObject.SetActive(false);

        if (costIconImage != null)
            costIconImage.gameObject.SetActive(false);

        _soldRoutine = null;
    }

    #endregion

    #region Visuals Setup (Outline + Selected Outline)

    private void SetupVisuals()
    {
        if (iconImage == null || iconImage.sprite == null) return;

        // Setup outline (always-on)
        if (outlineImage != null)
        {
            outlineImage.sprite = iconImage.sprite;
            outlineImage.color = outlineColor;
            CopyRectTransform(iconImage, outlineImage);
            outlineImage.transform.localScale = new Vector3(outlineScale, outlineScale, 1f);
            EnsureBehind(iconImage, outlineImage);
            outlineImage.gameObject.SetActive(true);
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
        if (_lockedOut)
            selected = false;

        // Toggle the selected outline image
        if (selectedImage != null)
            selectedImage.gameObject.SetActive(selected);
            
        if (outlineImage != null)
            outlineImage.gameObject.SetActive(!_lockedOut);

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

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_lockedOut) return;
        _onHover?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_lockedOut) return;
        _onHoverExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        HandleCardClicked();
    }

    private void HandleCardClicked()
    {
        if (_lockedOut) return;
        if (_lastClickFrame == Time.frameCount) return;

        _lastClickFrame = Time.frameCount;
        _onClick?.Invoke();
    }

    #endregion
}