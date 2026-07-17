using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PotManager — Full-screen pot opening sequence.
///
/// Two pot slots exist in each PlayerShopPanel (SmallPot + GrandPot).
/// Each has its own PotType SO defining cost, clicks, cards drawn, cards to keep.
///
/// Sequence:
///   1. Player buys a pot in the shop (ShopManager calls OpenSequence).
///   2. Full-screen overlay fades in showing the pot image.
///   3. Player clicks the pot N times (clicksToOpen) — sprite cycles each click.
///   4. On final click: explosion effect plays, cards slide in face-up.
///   5. Player clicks cards to select them (up to cardsToKeep).
///   6. Unselected cards lock out once quota is reached.
///   7. Player clicks Confirm — rewards applied server-side.
///   8. Overlay closes.
/// </summary>
public class PotManager : NetworkBehaviour
{
    public static PotManager Instance { get; private set; }

    #region Inspector Fields

    [Header("Pot Types — drag PotType SOs here")]
    public PotType smallPot;   // 3 draw, pick 1
    public PotType grandPot;   // 5 draw, pick 2

    [Header("Tarot Card Pool")]
    public List<TarotCardReward> cardPool = new List<TarotCardReward>();
    public Sprite                cardBackSprite;

    [Header("Overlay")]
    public GameObject  potOverlay;
    public CanvasGroup overlayCanvasGroup;
    public float       fadeInDuration  = 0.35f;

    [Header("Pot Graphic")]
    public Image       potImage;           // Shows clickSprites during clicking
    public GameObject  explodeEffect;      // Instantiated at explosion moment

    [Header("Pot Click Info")]
    public TMP_Text    clickHintText;      // "Click the pot to open it! (2 more clicks)"

    [Header("Card Area")]
    public Transform   cardArea;           // Parent for spawned TarotCardUI objects
    public GameObject  tarotCardPrefab;    // TarotCardUI prefab
    public float       cardDealInterval   = 0.15f;

    [Header("Selection UI")]
    public TMP_Text    selectionHintText;  // "Choose 2 cards"
    public Button      confirmButton;
    public TMP_Text    confirmButtonText;

    [Header("Close")]
    public Button      closeButton;

    [Header("Tooltip")]
    public GameObject  tooltipPrefab;      // Same CardTooltip prefab used in shop

    #endregion

    #region Private State

    private PotType              _currentPotType;
    private bool                 _potUsedSmall;
    private bool                 _potUsedGrand;

    private int                  _clicksRemaining;
    private bool                 _waitingForClicks;
    private bool                 _cardsDealt;
    private bool                 _sequenceRunning;

    private List<TarotCardReward> _dealtRewards = new List<TarotCardReward>();
    private List<TarotCardUI>     _spawnedCards = new List<TarotCardUI>();
    private List<TarotCardUI>     _selectedCards = new List<TarotCardUI>();
    private bool                  _autoResolvingSelection;

    private CardTooltip           _activeTooltip;
    private Canvas                _rootCanvas;

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        potOverlay?.SetActive(false);
        confirmButton?.gameObject.SetActive(false);
        closeButton?.gameObject.SetActive(false);
    }

    #endregion

    #region Phase Reset

    public void ResetForNewPhase()
    {
        _potUsedSmall = false;
        _potUsedGrand = false;
    }

    public bool IsSmallPotUsed => _potUsedSmall;
    public bool IsGrandPotUsed => _potUsedGrand;

    #endregion

    #region Open Entry Point

    /// <summary>Called by ShopManager after coins deducted. isGrand = which pot type.</summary>
    public void OpenSequence(bool isGrand)
    {
        if (_sequenceRunning)
            return;

        _currentPotType = isGrand ? grandPot : smallPot;
        if (_currentPotType == null)
        {
            Debug.LogError($"[PotManager] PotType not assigned ({(isGrand ? "grandPot" : "smallPot")})!");
            return;
        }

        if (isGrand) _potUsedGrand = true;
        else         _potUsedSmall = true;

        _sequenceRunning = true;
        StartCoroutine(RunPotSequence());
    }

    #endregion

    #region Pot Sequence Coroutine

    IEnumerator RunPotSequence()
    {
        _cardsDealt   = false;
        _waitingForClicks = false;
        _selectedCards.Clear();
        _autoResolvingSelection = false;
        ClearCards();
        DestroyTooltip();

        // Cache root canvas
        _rootCanvas = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas) { _rootCanvas = c; break; }

        // Show overlay
        if (potOverlay == null)
            Debug.LogWarning("[PotManager] potOverlay is not assigned, the pot UI will not be visible.");
        potOverlay?.SetActive(true);
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / fadeInDuration;
                overlayCanvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }

        // Set first sprite
        if (potImage && _currentPotType.clickSprites != null && _currentPotType.clickSprites.Length > 0)
            potImage.sprite = _currentPotType.clickSprites[0];

        potImage?.gameObject.SetActive(true);
        cardArea?.gameObject.SetActive(false);
        confirmButton?.gameObject.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        // Click phase
        _clicksRemaining = _currentPotType.clicksToOpen;
        _waitingForClicks = true;
        UpdateClickHint();

        yield return new WaitUntil(() => !_waitingForClicks);

        // Explosion
        yield return StartCoroutine(PlayExplosion());

        // Deal cards
        yield return StartCoroutine(DealCards());

        _cardsDealt = true;
        UpdateSelectionHint();

        confirmButton?.gameObject.SetActive(false);
        closeButton?.gameObject.SetActive(false);
    }

    #endregion

    #region Pot Click

    /// <summary>Wire this to the pot image's Button component.</summary>
    public void OnPotClicked()
    {
        if (!_waitingForClicks || _clicksRemaining <= 0) return;

        _clicksRemaining--;

        // Advance sprite
        if (potImage && _currentPotType.clickSprites != null)
        {
            int total   = _currentPotType.clickSprites.Length;
            int clicked = _currentPotType.clicksToOpen - _clicksRemaining;
            int idx     = Mathf.Clamp(clicked, 0, total - 1);
            potImage.sprite = _currentPotType.clickSprites[idx];
        }

        UpdateClickHint();

        if (_clicksRemaining <= 0)
            _waitingForClicks = false;
    }

    void UpdateClickHint()
    {
        if (clickHintText == null) return;
        if (_clicksRemaining > 0)
            clickHintText.text = _clicksRemaining == 1
                ? "One more click!"
                : $"Click the pot! ({_clicksRemaining} more)";
        else
            clickHintText.text = "";
    }

    #endregion

    #region Explosion

    IEnumerator PlayExplosion()
    {
        // Swap to explode sprite
        if (potImage && _currentPotType.explodeSprite)
            potImage.sprite = _currentPotType.explodeSprite;

        // Spawn effect
        if (_currentPotType.explodeEffect && potImage != null)
            Instantiate(_currentPotType.explodeEffect, potImage.transform.position, Quaternion.identity);

        yield return new WaitForSeconds(0.5f);

        potImage?.gameObject.SetActive(false);
    }

    #endregion

    #region Deal Cards

    IEnumerator DealCards()
    {
        ClearCards();
        _dealtRewards = PickRandomCards(_currentPotType.cardsToDraw);

        cardArea?.gameObject.SetActive(true);

        foreach (var reward in _dealtRewards)
        {
            if (tarotCardPrefab == null || cardArea == null) break;

            var go   = Instantiate(tarotCardPrefab, cardArea);
            var card = go.GetComponent<TarotCardUI>();
            if (card == null) continue;

            card.Setup(reward, cardBackSprite,
                onSelected:   OnCardSelected,
                onDeselected: OnCardDeselected,
                onHover:      OnCardHover);

            _spawnedCards.Add(card);
            yield return new WaitForSeconds(cardDealInterval);
        }
    }

    void ClearCards()
    {
        foreach (var c in _spawnedCards)
            if (c != null) Destroy(c.gameObject);
        _spawnedCards.Clear();
    }

    #endregion

    #region Card Selection

    void OnCardSelected(TarotCardUI card)
    {
        if (_autoResolvingSelection)
            return;

        if (_selectedCards.Contains(card)) return;
        _selectedCards.Add(card);

        if (_selectedCards.Count < _currentPotType.cardsToKeep)
        {
            UpdateSelectionHint();
            return;
        }

        _autoResolvingSelection = true;
        StartCoroutine(AutoResolveSelectedCards());
    }

    void OnCardDeselected(TarotCardUI card)
    {
        if (_autoResolvingSelection)
            return;

        _selectedCards.Remove(card);

        // Unlock all unselected cards
        foreach (var c in _spawnedCards)
            if (!c.IsSelected) c.SetLocked(false);

        UpdateSelectionHint();
        RefreshConfirmButton();
    }

    IEnumerator AutoResolveSelectedCards()
    {
        int picksToKeep = Mathf.Max(1, _currentPotType != null ? _currentPotType.cardsToKeep : 1);

        foreach (var c in _spawnedCards)
        {
            if (c == null) continue;
            bool keep = _selectedCards.Contains(c);
            c.SetLocked(!keep);
            c.SetInteractable(false);
        }

        UpdateSelectionHint();

        float wait = 0.15f;
        foreach (var c in _selectedCards)
        {
            if (c == null) continue;
            wait = Mathf.Max(wait, Mathf.Max(0.05f, c.flipDuration + 0.05f));
        }
        yield return new WaitForSeconds(wait);

        int applied = 0;
        foreach (var c in _selectedCards)
        {
            if (c == null || c.Reward == null) continue;
            ApplyTarotRewardRpc(c.Reward.name);
            applied++;
            if (applied >= picksToKeep)
                break;
        }

        OnCloseOverlay();
    }

    void UpdateSelectionHint()
    {
        if (selectionHintText == null || !_cardsDealt) return;
        int picksToKeep = Mathf.Max(1, _currentPotType != null ? _currentPotType.cardsToKeep : 1);
        int remaining = Mathf.Max(0, picksToKeep - _selectedCards.Count);

        if (_autoResolvingSelection)
            selectionHintText.text = "Applying selected cards...";
        else if (remaining > 0)
            selectionHintText.text = $"Pick {remaining} more card{(remaining == 1 ? "" : "s")}";
        else
            selectionHintText.text = "Applying selected cards...";
    }

    void RefreshConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        if (closeButton != null)
            closeButton.gameObject.SetActive(false);
    }

    #endregion

    #region Confirm

    public void OnConfirmClicked()
    {
        // Deprecated path: pot rewards now resolve instantly when a card is clicked.
    }

    [Rpc(SendTo.Server)]
    void ApplyTarotRewardRpc(string rewardName, RpcParams rpcParams = default)
    {
        ulong buyer  = rpcParams.Receive.SenderClientId;
        var player   = PlayerData.GetPlayer(buyer);
        var fx       = PlayerEffects.GetEffects(buyer);
        if (player == null) return;

        var reward = cardPool.Find(c => c != null && c.name == rewardName);
        if (reward == null) return;

        ApplyTarotEffect(reward, buyer, player, fx);
    }

    void ApplyTarotEffect(TarotCardReward reward, ulong buyer, PlayerData player, PlayerEffects fx)
    {
        float v = reward.effectValue > 0 ? reward.effectValue : 0.1f; // default 10%

        switch (reward.rewardType)
        {
            // ── Simple stat boosts ────────────────────────────────────────────
            case TarotRewardType.Chariot:
                player.SpeedMultiplier.Value += v;
                break;

            case TarotRewardType.Magician:
                player.JumpMultiplier.Value += v;
                break;

            case TarotRewardType.Empress:
                if (fx != null) fx.AttackSpeedMultiplier.Value += v;
                break;

            case TarotRewardType.HighPriestess:
                player.MaxHealthBonus.Value += v;
                break;

            case TarotRewardType.Star:
                // All stats up
                player.SpeedMultiplier.Value   += v;
                player.JumpMultiplier.Value    += v;
                player.DamageMultiplier.Value  += v;
                player.DefenseMultiplier.Value += v;
                player.MaxHealthBonus.Value    += v;
                break;

            case TarotRewardType.Tower:
                // Major health, decreased damage
                player.MaxHealthBonus.Value   += v * 2f;
                player.DamageMultiplier.Value -= v;
                break;

            case TarotRewardType.Hermit:
                // Major damage, decreased health
                player.DamageMultiplier.Value += v * 2f;
                player.MaxHealthBonus.Value   -= v;
                break;

            // ── Conditional damage ────────────────────────────────────────────
            case TarotRewardType.Emperor:
                // Activate Emperor flag — server re-evaluates coin standings
                if (fx != null)
                {
                    fx.EmperorActive.Value = true;
                    PlayerEffects.ServerUpdateEmperorStatus();
                }
                break;

            case TarotRewardType.World:
                // More damage at lower health
                if (fx != null) fx.WorldDamageBonus.Value += v;
                break;

            case TarotRewardType.Fool:
                // Super fast, attack cooldown 10s
                if (fx != null)
                {
                    fx.FoolActive.Value    = true;
                    fx.FoolSpeedMult.Value = 2.5f + v;
                }
                break;

            // ── Opponent-targeting ────────────────────────────────────────────
            case TarotRewardType.Hanged:
                if (fx != null)
                {
                    fx.HangedActive.Value = true;
                    fx.ServerApplyHangedMan(buyer);
                }
                break;

            case TarotRewardType.Lovers:
                // Pick two random opponents and link them
                ServerApplyLovers(buyer);
                break;

            // ── Aura / on-hit ─────────────────────────────────────────────────
            case TarotRewardType.Justice:
                if (fx != null) fx.ThornsDamagePercent.Value += v;
                break;

            case TarotRewardType.Sun:
                if (fx != null)
                {
                    fx.SunActive.Value    = true;
                    fx.SunAoeDamage.Value += v;
                }
                break;

            case TarotRewardType.Moon:
                if (fx != null) fx.MoonActive.Value = true;
                break;

            case TarotRewardType.Devil:
                if (fx != null) fx.LifestealPercent.Value += v;
                break;

            // ── Meta ──────────────────────────────────────────────────────────
            case TarotRewardType.WheelOfFortune:
                // Apply two random effects from the pool (excluding WheelOfFortune itself)
                var others = cardPool.FindAll(c =>
                    c != null && c.rewardType != TarotRewardType.WheelOfFortune);
                for (int i = others.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (others[i], others[j]) = (others[j], others[i]);
                }
                for (int i = 0; i < Mathf.Min(2, others.Count); i++)
                    ApplyTarotEffect(others[i], buyer, player, fx);
                break;

            case TarotRewardType.Coins:
                player.AddCoins((int)reward.effectValue);
                break;

            case TarotRewardType.Reroll:
                GrantFreeRerollRpc(RpcTarget.Single(buyer, RpcTargetUse.Temp));
                break;
        }
    }

    void ServerApplyLovers(ulong casterClientId)
    {
        // Pick two random opponents (not the caster)
        var opponents = new System.Collections.Generic.List<ulong>();
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            if (kvp.Key != casterClientId)
                opponents.Add(kvp.Key);

        if (opponents.Count < 2) return;

        int idxA = Random.Range(0, opponents.Count);
        int idxB;
        do { idxB = Random.Range(0, opponents.Count); } while (idxB == idxA);

        ulong partnerA = opponents[idxA];
        ulong partnerB = opponents[idxB];

        // Store the link on the caster's PlayerEffects so it can be read in combat
        var fx = PlayerEffects.GetEffects(casterClientId);
        if (fx != null)
        {
            fx.LoversPartnerA.Value = partnerA;
            fx.LoversPartnerB.Value = partnerB;
        }

        Debug.Log($"[Server] The Lovers: {partnerA} and {partnerB} now share health.");
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void GrantFreeRerollRpc(RpcParams rpcParams = default)
    {
        ulong local = NetworkManager.Singleton.LocalClientId;
        ShopManager.Instance?.GrantFreeReroll(local);
    }

    #endregion

    #region Close

    public void OnCloseOverlay()
    {
        potOverlay?.SetActive(false);
        ClearCards();
        DestroyTooltip();
        _selectedCards.Clear();
        _autoResolvingSelection = false;
        _sequenceRunning = false;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Tooltip

    void OnCardHover(TarotCardReward reward, bool enter)
    {
        if (!enter) { DestroyTooltip(); return; }
        if (tooltipPrefab == null || _rootCanvas == null) return;

        DestroyTooltip();
        var go = Instantiate(tooltipPrefab, _rootCanvas.transform);
        _activeTooltip = go.GetComponent<CardTooltip>();
        if (_activeTooltip == null) return;

        _activeTooltip.SetCanvas(_rootCanvas);

        // Populate with tarot card data
        if (_activeTooltip.nameText)   _activeTooltip.nameText.text   = reward.cardName;
        if (_activeTooltip.effectText) _activeTooltip.effectText.text = reward.rewardSummary;
        if (_activeTooltip.descText)   _activeTooltip.descText.text   = reward.flavorText;
        if (_activeTooltip.costText)   _activeTooltip.costText.text   = "";
        if (_activeTooltip.stockText)  _activeTooltip.stockText.text  = "";

        go.SetActive(true);

        // Find the specific card that owns this reward for positioning
        var hoveredCard = _spawnedCards.Find(c => c != null && c.Reward == reward);
        if (hoveredCard != null)
            StartCoroutine(PositionTooltipNextFrame(hoveredCard.GetComponent<RectTransform>()));
    }

    IEnumerator PositionTooltipNextFrame(RectTransform cardRect)
    {
        yield return null;
        if (_activeTooltip != null)
            _activeTooltip.PositionBesideCard(cardRect);
    }

    void DestroyTooltip()
    {
        if (_activeTooltip != null)
        {
            Destroy(_activeTooltip.gameObject);
            _activeTooltip = null;
        }
    }

    #endregion

    #region Helpers

    List<TarotCardReward> PickRandomCards(int count)
    {
        var pool   = new List<TarotCardReward>(cardPool);
        var result = new List<TarotCardReward>();

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < Mathf.Min(count, pool.Count); i++)
            result.Add(pool[i]);

        return result;
    }

    #endregion
}