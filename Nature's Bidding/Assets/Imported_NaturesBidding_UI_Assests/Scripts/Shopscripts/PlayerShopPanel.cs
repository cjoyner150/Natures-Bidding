using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PlayerShopPanel — One quarter of the shop screen, belonging to one player.
///
/// Local player panel: all cards and buttons are interactive.
/// Remote player panel: cards visible but buttons disabled — read-only view.
/// </summary>
public class PlayerShopPanel : MonoBehaviour
{
    #region Inspector Fields

    [Header("Panel Identity")]
    public Image    panelBackground;
    public Image    localPlayerBorder;
    public Color    localColor  = new Color(0.18f, 0.14f, 0.30f, 1f);
    public Color    remoteColor = new Color(0.09f, 0.08f, 0.13f, 1f);

    [Header("Header")]
    public TMP_Text playerNameText;
    public TMP_Text coinsText;

    [Header("Stat Display")]
    public TMP_Text mobilityText;
    public TMP_Text powerText;
    public TMP_Text defenseText;

    [Header("Slider Colors")]
    public Color positiveColor = Color.green;
    public Color negativeColor = Color.red;

    [Header("Cards — assign these in the prefab")]
    public Transform  cardsRow;             // Parent for the 3 upgrade cards
    public Transform  smallPotCardSlot;     // Parent for the small pot card
    public Transform  grandPotCardSlot;     // Parent for the grand pot card
    public GameObject upgradeCardPrefab;
    public GameObject potCardPrefab;

    [Header("Action Row")]
    public Button   rerollButton;
    public TMP_Text rerollButtonText;
    public float    rerollPullDistance = 36f;
    public float    rerollPullDownTime  = 0.12f;
    public float    rerollPullUpTime    = 0.16f;

    [Header("Tooltip (hover)")]
    public GameObject tooltipPrefab;        // CardTooltip prefab — spawned at cursor on hover

    [Header("Detail Panel")]
    public GameObject detailPanel;
    public TMP_Text   detailNameText;
    public TMP_Text   detailEffectText;
    public TMP_Text   detailCostText;
    public TMP_Text   detailStockText;
    public Button     buyButton;
    public TMP_Text   buyButtonText;

    [Header("Ready")]
    public Button   readyButton;
    public TMP_Text readyButtonText;

    #endregion

    #region Private State

    private ulong                   _clientId;
    private bool                    _isLocal;
    private PlayerData              _playerData;

    private List<UpgradeCardUI>     _upgradeCards   = new List<UpgradeCardUI>();
    private UpgradeCardUI           _smallPotCard;
    private UpgradeCardUI           _grandPotCard;
    private List<ShopUpgrade>       _offerings      = new List<ShopUpgrade>();

    private HashSet<ShopUpgrade>    _selectedUpgrades = new HashSet<ShopUpgrade>();
    private bool                    _smallPotSelected;
    private bool                    _grandPotSelected;
    private bool                    _smallPotUsed;
    private bool                    _grandPotUsed;
    private CardTooltip             _activeTooltip;
    private Canvas                  _canvas;
    private RectTransform           _rerollButtonRect;
    private Vector2                 _rerollButtonBasePosition;
    private Coroutine               _rerollAnimationRoutine;

    private Dictionary<string, int> _purchaseCounts = new Dictionary<string, int>();
    private bool                    _isPlaceholder;

    #endregion

    #region Setup

    public void Initialise(ulong clientId, List<ShopUpgrade> offerings, bool isLocal)
    {
        // Must be active before anything else — panel is spawned into an inactive canvas
        gameObject.SetActive(true);

        // Walk up to find the ROOT canvas — not a panel-level sub-canvas
        _canvas = GetComponentInParent<Canvas>();
        while (_canvas != null && !_canvas.isRootCanvas)
            _canvas = _canvas.transform.parent?.GetComponentInParent<Canvas>();
        if (_canvas == null)
            _canvas = FindFirstObjectByType<Canvas>();
        Debug.Log($"[PlayerShopPanel] Canvas found: {_canvas?.name} isRoot:{_canvas?.isRootCanvas}");

        _clientId  = clientId;
        _isLocal   = isLocal;
        _isPlaceholder = false;
        _offerings = offerings;
        _smallPotUsed = false;
        _grandPotUsed = false;

        if (panelBackground)   panelBackground.color = isLocal ? localColor : remoteColor;
        if (localPlayerBorder) localPlayerBorder.gameObject.SetActive(isLocal);

        if (detailPanel) detailPanel.SetActive(false);
        if (buyButton)   buyButton.gameObject.SetActive(false);

        if (rerollButton != null)
        {
            _rerollButtonRect = rerollButton.GetComponent<RectTransform>();
            if (_rerollButtonRect != null)
                _rerollButtonBasePosition = _rerollButtonRect.anchoredPosition;

            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() => { if (_isLocal) StartRerollChainPull(); });
            rerollButton.gameObject.SetActive(isLocal);
            rerollButton.interactable = isLocal;
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(isLocal);
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(() => ReadyManager.Instance?.OnReadyClicked());
            readyButton.interactable = isLocal;
        }
        if (readyButtonText != null)
            readyButtonText.text = "Ready";

        BuildCards();

        // Try PlayerData immediately — if not found yet, retry via coroutine
        // gameObject is now active so StartCoroutine will work
        _playerData = PlayerData.GetPlayer(clientId);
        if (_playerData != null)
            SubscribeAndRefresh();
        else
            StartCoroutine(RetryRefreshStats());
        
    }

    public void InitialisePlaceholder(string slotLabel, List<ShopUpgrade> offerings)
    {
        gameObject.SetActive(true);

        _canvas = GetComponentInParent<Canvas>();
        while (_canvas != null && !_canvas.isRootCanvas)
            _canvas = _canvas.transform.parent?.GetComponentInParent<Canvas>();
        if (_canvas == null)
            _canvas = FindFirstObjectByType<Canvas>();

        _clientId = 0;
        _isLocal = false;
        _isPlaceholder = true;
        _offerings = offerings ?? new List<ShopUpgrade>();
        _smallPotUsed = false;
        _grandPotUsed = false;

        if (panelBackground) panelBackground.color = remoteColor;
        if (localPlayerBorder) localPlayerBorder.gameObject.SetActive(false);

        if (playerNameText) playerNameText.text = slotLabel;
        if (coinsText) coinsText.text = "--";
        if (mobilityText) mobilityText.text = "--";
        if (powerText) powerText.text = "--";
        if (defenseText) defenseText.text = "--";

        if (detailPanel) detailPanel.SetActive(false);
        if (buyButton) buyButton.gameObject.SetActive(false);

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.gameObject.SetActive(false);
            rerollButton.interactable = false;
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.gameObject.SetActive(false);
            readyButton.interactable = false;
        }

        BuildCards();
    }
    void SubscribeAndRefresh()
    {
        if (_playerData == null) return;

        // Unsubscribe first to avoid double-subscribing on reroll
        UnsubscribeFromPlayerData();

        // Subscribe to all NetworkVariables so UI updates the moment server writes them
        _playerData.Coins.OnValueChanged           += OnDataChanged;
        _playerData.SpeedMultiplier.OnValueChanged  += OnDataChanged;
        _playerData.JumpMultiplier.OnValueChanged   += OnDataChanged;
        _playerData.DamageMultiplier.OnValueChanged += OnDataChanged;
        _playerData.DefenseMultiplier.OnValueChanged+= OnDataChanged;
        _playerData.MaxHealthBonus.OnValueChanged   += OnDataChanged;
        _playerData.PlayerName.OnValueChanged       += OnNameChanged;
        
        RefreshStats();
    }

    void UnsubscribeFromPlayerData()
    {
        if (_playerData == null) return;
        _playerData.Coins.OnValueChanged           -= OnDataChanged;
        _playerData.SpeedMultiplier.OnValueChanged  -= OnDataChanged;
        _playerData.JumpMultiplier.OnValueChanged   -= OnDataChanged;
        _playerData.DamageMultiplier.OnValueChanged -= OnDataChanged;
        _playerData.DefenseMultiplier.OnValueChanged-= OnDataChanged;
        _playerData.MaxHealthBonus.OnValueChanged   -= OnDataChanged;
        _playerData.PlayerName.OnValueChanged       -= OnNameChanged;
    }

    void OnDataChanged(float old, float newVal) => RefreshStats();
    void OnDataChanged(int old, int newVal)     => RefreshStats();
    void OnNameChanged(NetworkString old, NetworkString newVal) => RefreshStats();

    IEnumerator RetryRefreshStats()
    {
        if (_isPlaceholder)
            yield break;

        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForSeconds(0.25f);
            _playerData = PlayerData.GetPlayer(_clientId);
            if (_playerData != null)
            {
                SubscribeAndRefresh();
                yield break;
            }
        }
        Debug.LogWarning($"[PlayerShopPanel] Could not find PlayerData for client {_clientId} after retries.");
    }

    #endregion

    #region Reroll Animation

    void StartRerollChainPull()
    {
        if (!_isLocal)
            return;

        if (_rerollAnimationRoutine != null)
            StopCoroutine(_rerollAnimationRoutine);

        _rerollAnimationRoutine = StartCoroutine(RerollChainPullRoutine());
    }

    IEnumerator RerollChainPullRoutine()
    {
        if (rerollButton != null)
            rerollButton.interactable = false;

        if (_rerollButtonRect == null)
        {
            ShopManager.Instance?.LocalPlayerReroll();
            _rerollAnimationRoutine = null;
            yield break;
        }

        Vector2 startPosition = _rerollButtonBasePosition;
        Vector2 downPosition   = startPosition + Vector2.down * Mathf.Abs(rerollPullDistance);

        yield return MoveRerollButton(startPosition, downPosition, rerollPullDownTime);
        yield return MoveRerollButton(downPosition, startPosition, rerollPullUpTime);

        ShopManager.Instance?.LocalPlayerReroll();

        if (rerollButton != null)
            rerollButton.interactable = _isLocal;

        _rerollAnimationRoutine = null;
    }

    IEnumerator MoveRerollButton(Vector2 from, Vector2 to, float duration)
    {
        if (_rerollButtonRect == null)
            yield break;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            _rerollButtonRect.anchoredPosition = Vector2.LerpUnclamped(from, to, easedT);
            yield return null;
        }

        _rerollButtonRect.anchoredPosition = to;
    }

    #endregion

    #region Build Cards

    void BuildCards()
    {
        // Destroy old cards
        foreach (var c in _upgradeCards) { if (c) Destroy(c.gameObject); }
        _upgradeCards.Clear();
        if (_smallPotCard != null) { Destroy(_smallPotCard.gameObject); _smallPotCard = null; }
        if (_grandPotCard != null) { Destroy(_grandPotCard.gameObject); _grandPotCard = null; }

        if (_isPlaceholder)
            return;

        if (upgradeCardPrefab == null)
        {
            Debug.LogError($"[PlayerShopPanel] upgradeCardPrefab not assigned on {gameObject.name}");
            return;
        }
        if (cardsRow == null)
        {
            Debug.LogError($"[PlayerShopPanel] cardsRow not assigned on {gameObject.name}");
            return;
        }

        foreach (var upgrade in _offerings)
        {
            if (upgrade == null) continue;
            var go   = Instantiate(upgradeCardPrefab, cardsRow);
            var card = go.GetComponent<UpgradeCardUI>();
            if (card == null) continue;

            var cap  = upgrade;
            var capC = card;

            card.Populate(
                upgrade,
                GetOwned(upgrade),
                onClick:     () => OnUpgradeCardClicked(cap, capC),
                onHover:     () => OnUpgradeCardHovered(cap, capC),
                onHoverExit: () => OnCardHoverExit());
            _upgradeCards.Add(card);
        }

        if (smallPotCardSlot != null && potCardPrefab != null)
        {
            var go = Instantiate(potCardPrefab, smallPotCardSlot);
            _smallPotCard = go.GetComponent<UpgradeCardUI>();
            _smallPotCard?.SetPotCard(
                PotManager.Instance?.smallPot?.potName ?? "Small Pot",
                PotManager.Instance?.smallPot?.description ?? "Draw 3, pick 1",
                $"{ShopManager.SmallPotCost}",
                _smallPotUsed,
                onClick:     () => OnSmallPotClicked(),
                onHover:     () => OnSmallPotHovered(),
                onHoverExit: () => OnCardHoverExit());
        }

        if (grandPotCardSlot != null && potCardPrefab != null)
        {
            var go = Instantiate(potCardPrefab, grandPotCardSlot);
            _grandPotCard = go.GetComponent<UpgradeCardUI>();
            _grandPotCard?.SetPotCard(
                PotManager.Instance?.grandPot?.potName ?? "Grand Pot",
                PotManager.Instance?.grandPot?.description ?? "Draw 5, pick 2",
                $"{ShopManager.GrandPotCost}",
                _grandPotUsed,
                onClick:     () => OnGrandPotClicked(),
                onHover:     () => OnGrandPotHovered(),
                onHoverExit: () => OnCardHoverExit());
        }
    }

    public void ApplyNewOfferings(List<ShopUpgrade> newOfferings)
    {
        _offerings = newOfferings;
        _selectedUpgrades.Clear();
        _smallPotSelected = false;
        _grandPotSelected = false;
        DestroyTooltip();
        if (buyButton) buyButton.gameObject.SetActive(false);
        BuildCards();
    }

    #endregion

    #region Card Selection

    // ── Tooltip on hover ─────────────────────────────────────────────────────

    void OnUpgradeCardHovered(ShopUpgrade upgrade, UpgradeCardUI card)
    {
        if (!_isLocal) return;
        EnsureTooltip();
        _activeTooltip?.Populate(upgrade, GetOwned(upgrade));
        _activeTooltip?.gameObject.SetActive(true);
        StartCoroutine(PositionTooltipNextFrame(card.GetComponent<RectTransform>()));
    }

    void OnSmallPotHovered()
    {
        if (!_isLocal) return;
        EnsureTooltip();
        _activeTooltip?.PopulatePot(ShopManager.SmallPotCost, _smallPotUsed);
        _activeTooltip?.gameObject.SetActive(true);
        StartCoroutine(PositionTooltipNextFrame(_smallPotCard?.GetComponent<RectTransform>()));
    }

    void OnGrandPotHovered()
    {
        if (!_isLocal) return;
        EnsureTooltip();
        _activeTooltip?.PopulatePot(ShopManager.GrandPotCost, _grandPotUsed);
        _activeTooltip?.gameObject.SetActive(true);
        StartCoroutine(PositionTooltipNextFrame(_grandPotCard?.GetComponent<RectTransform>()));
    }

    IEnumerator PositionTooltipNextFrame(RectTransform cardRect)
    {
        // Wait one frame so the layout system has built the tooltip's RectTransform
        // before we try to read its sizeDelta for edge detection
        yield return null;
        _activeTooltip?.PositionBesideCard(cardRect);
    }

    void OnCardHoverExit()
    {
        if (_activeTooltip != null)
            _activeTooltip.gameObject.SetActive(false);
    }

    /// <summary>Creates the tooltip once and reuses it — avoids flash from destroy/recreate.</summary>
    void EnsureTooltip()
    {
        if (_activeTooltip != null) return;
        if (tooltipPrefab == null)  return;

        // Re-find canvas here in case it was null during Initialise
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            // Walk up to root
            Canvas c = _canvas;
            while (c != null && !c.isRootCanvas)
            {
                Canvas parent = c.transform.parent?.GetComponentInParent<Canvas>();
                if (parent == null) break;
                c = parent;
            }
            _canvas = c;
        }

        // Last resort — find any root canvas in the scene
        if (_canvas == null)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.isRootCanvas) { _canvas = c; break; }
            }
        }

        if (_canvas == null) { Debug.LogError("[PlayerShopPanel] Cannot find any Canvas!"); return; }

        var go = Instantiate(tooltipPrefab, _canvas.transform);
        go.SetActive(false);
        _activeTooltip = go.GetComponent<CardTooltip>();
        if (_activeTooltip != null) _activeTooltip.SetCanvas(_canvas);
    }

    void DestroyTooltip()
    {
        if (_activeTooltip != null)
        {
            Destroy(_activeTooltip.gameObject);
            _activeTooltip = null;
        }
    }

    // ── Click to select / deselect ───────────────────────────────────────────

    void OnUpgradeCardClicked(ShopUpgrade upgrade, UpgradeCardUI card)
    {
        if (!_isLocal || upgrade == null) return;

        int owned = GetOwned(upgrade);

        // Prevent buying beyond max
        if (owned >= upgrade.maxPurchases)
            return;

        // Immediately purchase on click instead of waiting for the Buy button.
        ShopManager.Instance?.LocalPlayerBuyUpgrade(upgrade, this);
    }

    void OnSmallPotClicked()
    {
        if (!_isLocal || _smallPotUsed) return;

        Debug.Log("[PlayerShopPanel] Small Pot clicked, requesting purchase/open.");
        ShopManager.Instance?.LocalPlayerBuyPot(this, false);
    }

    void OnGrandPotClicked()
    {
        if (!_isLocal || _grandPotUsed) return;

        Debug.Log("[PlayerShopPanel] Grand Pot clicked, requesting purchase/open.");
        ShopManager.Instance?.LocalPlayerBuyPot(this, true);
    }

    void ClearAllSelections()
    {
        _smallPotSelected = false;
        _grandPotSelected = false;
        _smallPotCard?.SetSelected(false);
        _grandPotCard?.SetSelected(false);
        foreach (var c in _upgradeCards) c?.SetSelected(false);
    }

    #endregion

    #region Buy Button Refresh

    /// <summary>Updates the buy button based on what is currently selected.</summary>
    void RefreshBuyButton()
    {
        if (_isPlaceholder) return;
        if (buyButton == null) return;

        if (_smallPotSelected)
        {
            int  cost      = ShopManager.SmallPotCost;
            bool canAfford = GetCoins() >= cost;
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = canAfford && !_smallPotUsed;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
            if (buyButtonText)
                buyButtonText.text = _smallPotUsed ? "Already Used"
                    : !canAfford    ? "Can't Afford"
                    :                 $"Open  {cost} coins";
            return;
        }
        if (_grandPotSelected)
        {
            int  cost      = ShopManager.GrandPotCost;
            bool canAfford = GetCoins() >= cost;
            buyButton.gameObject.SetActive(true);
            buyButton.interactable = canAfford && !_grandPotUsed;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyClicked);
            if (buyButtonText)
                buyButtonText.text = _grandPotUsed ? "Already Used"
                    : !canAfford    ? "Can't Afford"
                    :                 $"Open  {cost} coins";
            return;
        }

        if (_selectedUpgrades.Count == 0)
        {
            buyButton.gameObject.SetActive(false);
            return;
        }

        // Calculate total cost of all selected upgrades
        int total     = 0;
        bool anyValid = false;
        foreach (var u in _selectedUpgrades)
        {
            int owned = GetOwned(u);
            if (owned < u.maxPurchases) { total += u.cost; anyValid = true; }
        }

        bool canAffordAll = GetCoins() >= total;

        buyButton.gameObject.SetActive(true);
        buyButton.interactable = canAffordAll && anyValid;
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuyClicked);

        if (buyButtonText)
        {
            string label = _selectedUpgrades.Count == 1
                ? $"Buy  {total} coins"
                : $"Buy {_selectedUpgrades.Count} items  {total} coins";
            buyButtonText.text = canAffordAll ? label : "Can't Afford";
        }
    }

    #endregion

    #region Buy

    void OnBuyClicked()
    {
        if (_smallPotSelected && !_smallPotUsed)
        {
            ShopManager.Instance?.LocalPlayerBuyPot(this, false);
            return;
        }
        if (_grandPotSelected && !_grandPotUsed)
        {
            ShopManager.Instance?.LocalPlayerBuyPot(this, true);
            return;
        }

        // Buy every selected upgrade
        var toBuy = new List<ShopUpgrade>(_selectedUpgrades);
        foreach (var upgrade in toBuy)
            ShopManager.Instance?.LocalPlayerBuyUpgrade(upgrade, this);
    }

    #endregion

    #region Server-side State Updates

    public void OnUpgradePurchased(string upgradeId)
    {
        DestroyTooltip();

        if (!_purchaseCounts.ContainsKey(upgradeId))
            _purchaseCounts[upgradeId] = 0;
        _purchaseCounts[upgradeId]++;

        // Find the purchased card and keep it in the layout while disabling interaction
        UpgradeCardUI purchasedCard = null;
        ShopUpgrade   purchasedUpgrade = null;
        foreach (var card in _upgradeCards)
        {
            if (card != null && card.Upgrade != null && card.Upgrade.Id == upgradeId)
            {
                purchasedCard    = card;
                purchasedUpgrade = card.Upgrade;
                break;
            }
        }

        if (purchasedCard != null)
        {
            purchasedCard.SetLockedOut("Bought");
        }

        if (purchasedUpgrade != null)
            _selectedUpgrades.Remove(purchasedUpgrade);

        ClearAllSelections();
        RefreshBuyButton();
        RefreshStats();
    }

    public void OnPotUsed(bool isGrand)
    {
        DestroyTooltip();

        if (isGrand)
        {
            _grandPotUsed = true;
            if (_grandPotCard != null)
            {
                Destroy(_grandPotCard.gameObject);
                _grandPotCard = null;
            }
        }
        else
        {
            _smallPotUsed = true;
            if (_smallPotCard != null)
            {
                Destroy(_smallPotCard.gameObject);
                _smallPotCard = null;
            }
        }

        ClearAllSelections();
        if (detailPanel) detailPanel.SetActive(false);
        if (buyButton)   buyButton.gameObject.SetActive(false);
    }

    #endregion

    #region Stats

    public void RefreshStats()
    {
        if (_isPlaceholder) return;

        _playerData = PlayerData.GetPlayer(_clientId);
        if (_playerData == null) return;

        if (playerNameText)
            playerNameText.text = _playerData.PlayerName.Value.Value;

        if (coinsText)
            coinsText.text = $"{_playerData.Coins.Value}";

        // ---- COMBINE STATS ----

        float mobility = (_playerData.SpeedMultiplier.Value +
                        _playerData.JumpMultiplier.Value) * 0.5f;

        float power = _playerData.DamageMultiplier.Value;
        float defense = _playerData.DefenseMultiplier.Value;

        // ---- TEXT ----
        if (mobilityText)
            mobilityText.text = $"{FormatPlusStat(mobility)}";

        if (powerText)
            powerText.text = $"{FormatPlusStat(power)}";

        if (defenseText)
            defenseText.text = $"{FormatPlusStat(defense)}";
    }

    string FormatPlusStat(float multiplier)
    {
        int bonusPoints = Mathf.Max(0, Mathf.RoundToInt((multiplier - 1f) * 100f));
        int plusCount   = bonusPoints / 10;

        if (plusCount <= 0)
            return "Base";

        if (plusCount <= 3)
            return new string('+', plusCount);

        return $"+{plusCount}";
    }

    #endregion

    #region Helpers

    void OnDestroy()
    {
        UnsubscribeFromPlayerData();
        DestroyTooltip();
    }

    int GetOwned(ShopUpgrade upgrade)
    {
        if (upgrade == null) return 0;
        _purchaseCounts.TryGetValue(upgrade.Id, out int c);
        return c;
    }

    int GetCoins()
    {
        if (_isPlaceholder) return 0;
        var inv = PlayerInventory.Local;
        return inv != null ? inv.Coins : 0;
    }

    #endregion
}