using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// TarotCardUI — One tarot card in the pot opening screen.
/// Click to select/deselect. Shows tooltip on hover.
/// Locked out when enough cards already selected.
/// </summary>
public class TarotCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    #region Inspector Fields

    [Header("Card Visuals")]
    public Image      cardImage;
    public Image      glowOutline;
    public Image      lockedOverlay;

    #endregion

    #region Private State

    private TarotCardReward              _reward;
    private Sprite                       _backSprite;
    private bool                         _selected;
    private bool                         _locked;
    private bool                         _isFaceUp;
    private bool                         _isHovered;
    private Action<TarotCardUI>          _onSelected;
    private Action<TarotCardUI>          _onDeselected;
    private Action<TarotCardReward,bool> _onHover;
    private Button                       _button;

    #endregion

    #region Lifecycle

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(OnClicked);
        glowOutline?.gameObject.SetActive(false);
        lockedOverlay?.gameObject.SetActive(false);
    }

    #endregion

    #region Setup

    public void Setup(
        TarotCardReward reward,
        Sprite backSprite,
        Action<TarotCardUI> onSelected,
        Action<TarotCardUI> onDeselected,
        Action<TarotCardReward,bool> onHover)
    {
        _reward       = reward;
        _backSprite   = backSprite;
        _onSelected   = onSelected;
        _onDeselected = onDeselected;
        _onHover      = onHover;
        _selected     = false;
        _locked       = false;
        _isFaceUp     = false;
        _isHovered    = false;

        if (cardImage && backSprite) cardImage.sprite = backSprite;
        glowOutline?.gameObject.SetActive(false);
        lockedOverlay?.gameObject.SetActive(false);
        SetInteractable(true);
    }

    #endregion

    #region Flip Animation

    [Header("Flip Animation")]
    public float flipDuration = 0.2f;
    private Coroutine _flipRoutine;

    private IEnumerator FlipToSide(bool toFront)
    {
        if (_flipRoutine != null) yield break;
        if (cardImage == null) yield break;

        Transform cardTransform = cardImage.transform;
        Vector3 originalScale = cardTransform.localScale;
        float elapsed = 0f;

        // Phase 1: shrink width to 0
        while (elapsed < flipDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flipDuration / 2f);
            float scaleX = Mathf.Lerp(originalScale.x, 0f, t);
            cardTransform.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }
        cardTransform.localScale = new Vector3(0f, originalScale.y, originalScale.z);

        // Swap sprite
        if (toFront && _reward != null && _reward.cardFaceSprite != null)
            cardImage.sprite = _reward.cardFaceSprite;
        else if (_backSprite != null)
            cardImage.sprite = _backSprite;

        // Phase 2: expand back
        elapsed = 0f;
        while (elapsed < flipDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flipDuration / 2f);
            float scaleX = Mathf.Lerp(0f, originalScale.x, t);
            cardTransform.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }
        cardTransform.localScale = originalScale;
        _isFaceUp = toFront;
        glowOutline?.gameObject.SetActive(false);

        _flipRoutine = null;
    }

    #endregion

    #region Click Selection

    void OnClicked()
    {
        if (_locked && !_selected) return;

        if (_selected)
        {
            _selected = false;
            glowOutline?.gameObject.SetActive(false);

            if (!_isHovered && _flipRoutine == null && _isFaceUp)
                _flipRoutine = StartCoroutine(FlipToSide(false));

            _onDeselected?.Invoke(this);
        }
        else
        {
            if (_flipRoutine == null && !_isFaceUp)
                _flipRoutine = StartCoroutine(FlipToSide(true));
            _selected = true;
            _onSelected?.Invoke(this);
        }
    }

    public void SetLocked(bool locked)
    {
        _locked = locked;
        lockedOverlay?.gameObject.SetActive(locked && !_selected);
        if (_button != null) _button.interactable = !locked || _selected;
    }

    public void SetInteractable(bool on)
    {
        if (_button != null) _button.interactable = on;
    }

    public bool            IsSelected => _selected;
    public TarotCardReward Reward     => _reward;

    #endregion

    #region Hover Tooltip

    public void OnPointerEnter(PointerEventData e)
    {
        Debug.Log($"[TarotCardUI] OnPointerEnter at {Time.time:F3}, flipRoutine active={_flipRoutine != null}, isFaceUp={_isFaceUp}");
        _isHovered = true;
        if (!_selected && !_locked && !_isFaceUp && _flipRoutine == null)
            _flipRoutine = StartCoroutine(FlipToSide(true));

        if (_reward != null) _onHover?.Invoke(_reward, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        Debug.Log($"[TarotCardUI] OnPointerExit at {Time.time:F3}, flipRoutine active={_flipRoutine != null}, isFaceUp={_isFaceUp}");
        _isHovered = false;

        if (_reward != null) _onHover?.Invoke(_reward, false);
    }

    #endregion
}