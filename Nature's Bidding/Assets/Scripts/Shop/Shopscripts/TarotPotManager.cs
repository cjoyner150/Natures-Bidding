using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// TarotPotManager — Full-screen pot opening sequence.
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
public class TarotPotManager : NetworkBehaviour
{
    public static TarotPotManager Instance { get; private set; }

    public static Action<TarotPotUIBehaviour> OnPotUIHoveredEvent;
    public static Action<TarotPotUIBehaviour> OnPotUIClickedEvent;

    #region Inspector Fields

    [Header("Pot Selection UI")]
    [SerializeField] private GameObject playerSelectionVisualPrefab;

    [Header("Pot Types — drag PotType SOs here")]
    [SerializeField] private TarotPotUIBehaviour[] potUIBehaviours;
    private Dictionary<PotSize, TarotPotUIBehaviour> potUILookup = new Dictionary<PotSize, TarotPotUIBehaviour>();

    [Header("Tarot Card Pool")]
    [SerializeField] private List<TarotCardReward> cardPool = new List<TarotCardReward>();
    [SerializeField] private Sprite cardBackSprite;

    [Header("Overlay")]
    [SerializeField] private GameObject  potOverlay;
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private float       fadeInDuration  = 0.35f;

    [Header("Pot Graphic")]
    [SerializeField] private Image       potImage;           // Shows clickSprites during clicking
    [SerializeField] private GameObject  explodeEffect;      // Instantiated at explosion moment

    [Header("Pot Click Info")]
    [SerializeField] private TMP_Text    clickHintText;      // "Click the pot to open it! (2 more clicks)"

    [Header("Card Area")]
    [SerializeField] private Transform   cardArea;           // Parent for spawned TarotCardUI objects
    [SerializeField] private GameObject  tarotCardPrefab;    // TarotCardUI prefab
    [SerializeField] private float       cardDealInterval   = 0.15f;

    [Header("Selection UI")]
    [SerializeField] private TMP_Text    selectionHintText;  // "Choose 2 cards"
    [SerializeField] private Button      confirmButton;
    [SerializeField] private TMP_Text    confirmButtonText;

    [Header("Close")]
    [SerializeField] private Button      closeButton;

    [Header("Tooltip")]
    [SerializeField] private GameObject  tooltipPrefab;      // Same CardTooltip prefab used in shop
    [SerializeField] private GameObject playerCrosshairPrefab;


    #endregion

    #region Private State

    private Dictionary<ulong, TarotPotUIBehaviour> playerPotSelections = new Dictionary<ulong, TarotPotUIBehaviour>();
    private Dictionary<ulong, Image> playerSelectionVisuals = new Dictionary<ulong, Image>();
    private bool allowSelect = true;

    private PotType              _currentPurchasedPotType;
    private int                  _clicksRemaining;
    private bool                 _waitingForClicks;
    private bool                 _cardsDealt;
    private bool                 _sequenceRunning;

    private List<TarotCardReward> _dealtRewards = new List<TarotCardReward>();
    private List<TarotCardUI>     _spawnedCards = new List<TarotCardUI>();
    private List<TarotCardUI>     _selectedCards = new List<TarotCardUI>();
    private bool                  _autoResolvingSelection;

    private CardTooltip           _activeTooltip;
    [SerializeField] private Canvas                _rootCanvas;

    private object _activeHoverTarget;

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        potUILookup.Clear();
        foreach (var potUI in potUIBehaviours)
        {
            if (potUI == null) continue;
            if (!potUILookup.ContainsKey(potUI.PotType.potSize))
                potUILookup.Add(potUI.PotType.potSize, potUI);
        }

        HookEvents();
    }

    private void HookEvents()
    {
        OnPotUIClickedEvent += OnPotUIClicked;
        OnPotUIHoveredEvent += OnPotUIHovered;
    }

    private void UnhookEvents()
    {
        OnPotUIClickedEvent -= OnPotUIClicked;
        OnPotUIHoveredEvent -= OnPotUIHovered;
    }

    public async override void OnNetworkSpawn()
    {
        potOverlay?.SetActive(false);
        confirmButton?.gameObject.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        var flowManager = PersistentGameStateManager.Instance;
        if (flowManager != null)
        {
            var readyManager = ReadyManager.Instance != null ? ReadyManager.Instance : FindAnyObjectByType<ReadyManager>();

            await SceneReadiness.WaitForAllPlayersLoaded();

            flowManager.OnTarotSceneReady();

            if (IsServer)
                flowManager.BeginTarotPhaseServer();
        }
    }

    public void OnTarotPhaseStart()
    {
        GameLogger.Log(LogSeverity.Info, "Tarot phase is starting...");

        SpawnAllPlayerCrosshairs();
    }

    public void SpawnAllPlayerCrosshairs()
    {
        if (!IsServer) return;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            GameObject crosshairGO = Instantiate(playerCrosshairPrefab);
            NetworkObject netObj = crosshairGO.GetComponent<NetworkObject>();
            netObj.SpawnWithOwnership(clientId);
        }
    }

    #endregion

    #region Card Pointer Hooks

    public void OnPotUIClicked(TarotPotUIBehaviour potBehaviour)
    {
        GameLogger.Log(LogSeverity.Debug, $"{potBehaviour.PotType.potSize} clicked, requesting purchase/open.");
        TrySelectPot(potBehaviour);
    }

    public void OnPotUIHovered(TarotPotUIBehaviour potBehaviour)
    {
        _hideTooltipCts?.Cancel();
        _hideTooltipCts?.Dispose();
        _hideTooltipCts = null;

        if (_activeTooltip != null && _activeHoverTarget as string == "SmallPot")
            return;

        EnsureTooltip();
        _activeTooltip?.PopulatePot(potBehaviour.PotType.cost);
        _activeHoverTarget = "SmallPot";
        ShowTooltipNextFrameAsync(potBehaviour.GetComponent<RectTransform>()).Forget();
    }

    #endregion

    #region Pot Selection Handling

    private void TrySelectPot(TarotPotUIBehaviour potBehaviour)
    {
        if (!allowSelect) return;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (potBehaviour == null) return;

        if (playerPotSelections.TryGetValue(localClientId, out var potSelection) && potSelection == potBehaviour) return;

        PlayerData playerData = PersistentPlayerRegistry.Instance?.GetByClientId(localClientId);
        if (playerData == null)
        {
            GameLogger.Log(LogSeverity.Warning, $"TrySelectPot rejected: player data not found for local client {localClientId}.");
            return;
        }

        bool canAfford = playerData.gold >= potBehaviour.PotType.cost; // Locally update the selection visual for immediate feedback, server will confirm later
        if (canAfford)
        {
            UpdateLocalPlayerSelection(localClientId, potBehaviour);
        }

        if (IsServer) // Skip request if we're the server, just broadcast the selection to everyone
        {
            NotifyEveryonePlayerSelectionClientRpc(potBehaviour.PotType.potSize, canAfford, localClientId);
            return;
        }

        RequestSelectPotServerRpc(potBehaviour.PotType.potSize);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSelectPotServerRpc(PotSize potSize, RpcParams rpcParams = default)
    {
        if (!allowSelect) return;

        PlayerData playerData = PersistentPlayerRegistry.Instance?.GetByClientId(rpcParams.Receive.SenderClientId);
        if (playerData == null)
        {
            GameLogger.Log(LogSeverity.Warning, $"RequestSelectPotServerRpc rejected for client {rpcParams.Receive.SenderClientId}: player data not found.");
            return;
        }

        // Server has authority on success
        var potType = GetPotTypeBySize(potSize);
        bool success = playerData.gold >= potType.cost;

        // Tell everyone to update their selections
        NotifyEveryonePlayerSelectionClientRpc(potSize, success, rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyEveryonePlayerSelectionClientRpc(PotSize potSize, bool success, ulong playerClientId)
    {
        if (!allowSelect) return;
        TarotPotUIBehaviour tarotPotUIBehaviour = GetPotUIBySize(potSize);

        if (success) 
            UpdateLocalPlayerSelection(playerClientId, tarotPotUIBehaviour);
        else if (playerPotSelections.TryGetValue(playerClientId, out var potSelection) && potSelection == tarotPotUIBehaviour) 
            UpdateLocalPlayerSelection(playerClientId, null);

        if (IsServer)
        {
            var players = PersistentPlayerRegistry.Instance?.GetAllPlayers();
            var selections = playerPotSelections.Values.Where(p => p != null).ToList();

            if (selections.Count == players.Count)
            {
                // All players have made their selections

                // Ensure all local players selection state matches their finalized choice
                foreach (var kvp in playerPotSelections)
                {
                    TarotPotUIBehaviour pot = kvp.Value;
                    ulong clientId = kvp.Key;

                    NotifyFinalizedSelectionClientRpc(pot.PotType.potSize, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                }
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyFinalizedSelectionClientRpc(PotSize potSize, RpcParams rpcParams)
    {
        UpdateLocalPlayerSelection(NetworkManager.LocalClientId, GetPotUIBySize(potSize)); // Ensure the local player's selection visual is updated for the finalized selection
        allowSelect = false; // This needs to be checked wherever a client rpc can land so call timing does not override final selection

        BuyPotServerRpc(potSize);
    }

    private void UpdateLocalPlayerSelection(ulong playerClientId, TarotPotUIBehaviour potBehaviour)
    {
        playerPotSelections.TryGetValue(playerClientId, out TarotPotUIBehaviour currentSelection);
        
        if (currentSelection != null && currentSelection == potBehaviour) return;

        if (playerSelectionVisuals.TryGetValue(playerClientId, out Image currentSelectionVisual) && currentSelectionVisual != null)
        {
            Destroy(currentSelectionVisual.gameObject);
            playerSelectionVisuals[playerClientId] = null;
        }

        playerPotSelections[playerClientId] = potBehaviour;
        playerSelectionVisuals[playerClientId] = potBehaviour != null ? SpawnPlayerSelectionVisual(playerClientId, potBehaviour.LayoutGroup.transform) : null;
    }

    private Image SpawnPlayerSelectionVisual(ulong clientId, Transform parent)
    {
        var go = Instantiate(playerSelectionVisualPrefab, parent);

        Image img = go?.GetComponent<Image>();
        if (img == null) { 
            GameLogger.Log(LogSeverity.Error, "Player selection visual failed to spawn. Image not found.");
            return null;
        }

        img.color = PersistentPlayerRegistry.Instance.GetPlayerColor(clientId);
        return img;
    }


    #endregion

    #region End Selection Sequence



    #endregion

    #region Economy

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void BuyPotServerRpc(PotSize potSize, RpcParams rpcParams = default)
    {
        ulong buyer = rpcParams.Receive.SenderClientId;
        var registry = PersistentPlayerRegistry.Instance;
        var playerState = registry?.GetByClientId(buyer);
        if (registry == null || playerState == null)
        {
            GameLogger.Log(LogSeverity.Warning, $"BuyPotRpc rejected for client {buyer}: persistent registry data not found.");
            return;
        }

        var pot = GetPotTypeBySize(potSize);
        if (pot == null)
        {
            GameLogger.Log(LogSeverity.Error, $"BuyPotRpc failed for client {buyer}: pot type not found for size {potSize}.");
            return;
        }

        int cost = pot.cost;

        if (playerState.gold < cost)
        {
            GameLogger.Log(LogSeverity.Warning, $"BuyPotRpc rejected for client {buyer}: not enough coins ({playerState.gold}/{cost}).");
            return;
        }

        GameLogger.Log(LogSeverity.Debug, $"BuyPotRpc accepted for client {buyer}. Deducting {cost} and opening {(potSize == PotSize.grandPot ? "Grand" : "Small")} pot.");

        if (!registry.TrySpendGold(buyer, cost))
            return;

        NotifyPotUsedClientRpc(buyer, potSize);
        NotifyPurchaseSuccessClientRpc(potSize, RpcTarget.Single(buyer, RpcTargetUse.Temp));
    }

    /// <summary>Broadcast so all panels showing this player mark pot as used.</summary>
    [Rpc(SendTo.Everyone)]
    void NotifyPotUsedClientRpc(ulong buyer, PotSize potSize)
    {
        // TODO: Sync pot usage
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void NotifyPurchaseSuccessClientRpc(PotSize potSize, RpcParams rpcParams = default)
    {
        GameLogger.Log(LogSeverity.Debug, $"Opening pot UI sequence on client. potSize:{potSize}");
        OpenSequence(potSize);
    }

    #endregion

    #region Open Entry Point

    /// <summary>Called by ShopManager after coins deducted. isGrand = which pot type.</summary>
    public void OpenSequence(PotSize potSize)
    {
        if (_sequenceRunning)
            return;

        _currentPurchasedPotType = GetPotTypeBySize(potSize);
        if (_currentPurchasedPotType == null)
        {
            GameLogger.Log(LogSeverity.Error, $"PotType not assigned ({potSize})!");
            return;
        }

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

        //// Cache root canvas
        //_rootCanvas = null;
        //foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        //    if (c.isRootCanvas) { _rootCanvas = c; break; }
        GameLogger.Log(LogSeverity.Verbose, $"root canvas is {_rootCanvas == null}");

        // Show overlay
        if (potOverlay == null)
            GameLogger.Log(LogSeverity.Warning, "potOverlay is not assigned, the pot UI will not be visible.");
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
        if (potImage && _currentPurchasedPotType.clickSprites != null && _currentPurchasedPotType.clickSprites.Length > 0)
            potImage.sprite = _currentPurchasedPotType.clickSprites[0];

        potImage?.gameObject.SetActive(true);
        cardArea?.gameObject.SetActive(false);
        confirmButton?.gameObject.SetActive(false);
        closeButton?.gameObject.SetActive(false);

        // Click phase
        _clicksRemaining = _currentPurchasedPotType.clicksToOpen;
        _waitingForClicks = true;
        UpdateAnimatedClickHint();

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

    #region Animated Pot Click

    /// <summary>Wire this to the pot image's Button component.</summary>
    public void OnAnimatedPotClicked()
    {
        if (!_waitingForClicks || _clicksRemaining <= 0) return;

        _clicksRemaining--;

        // Advance sprite
        if (potImage && _currentPurchasedPotType.clickSprites != null)
        {
            int total   = _currentPurchasedPotType.clickSprites.Length;
            int clicked = _currentPurchasedPotType.clicksToOpen - _clicksRemaining;
            int idx     = Mathf.Clamp(clicked, 0, total - 1);
            potImage.sprite = _currentPurchasedPotType.clickSprites[idx];
        }

        UpdateAnimatedClickHint();

        if (_clicksRemaining <= 0)
            _waitingForClicks = false;
    }

    void UpdateAnimatedClickHint()
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
        if (potImage && _currentPurchasedPotType.explodeSprite)
            potImage.sprite = _currentPurchasedPotType.explodeSprite;

        // Spawn effect
        if (_currentPurchasedPotType.explodeEffect && potImage != null)
            Instantiate(_currentPurchasedPotType.explodeEffect, potImage.transform.position, Quaternion.identity);

        yield return new WaitForSeconds(0.5f);

        potImage?.gameObject.SetActive(false);
    }

    #endregion

    #region Deal Cards

    IEnumerator DealCards()
    {
        ClearCards();
        _dealtRewards = PickRandomCards(_currentPurchasedPotType.cardsToDraw);

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

        if (_selectedCards.Count < _currentPurchasedPotType.cardsToKeep)
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
        int picksToKeep = Mathf.Max(1, _currentPurchasedPotType != null ? _currentPurchasedPotType.cardsToKeep : 1);

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
            string rewardId = !string.IsNullOrWhiteSpace(c.Reward.cardId)
                ? c.Reward.cardId
                : c.Reward.name;
            ApplyTarotRewardRpc(rewardId);
            applied++;
            if (applied >= picksToKeep)
                break;
        }

        OnCloseOverlay();
    }

    void UpdateSelectionHint()
    {
        if (selectionHintText == null || !_cardsDealt) return;
        int picksToKeep = Mathf.Max(1, _currentPurchasedPotType != null ? _currentPurchasedPotType.cardsToKeep : 1);
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
    void ApplyTarotRewardRpc(string rewardId, RpcParams rpcParams = default)
    {
        ulong buyer  = rpcParams.Receive.SenderClientId;
        var registry = PersistentPlayerRegistry.Instance;
        if (registry == null) return;

        var reward = cardPool.Find(c => c != null && (c.cardId == rewardId || c.name == rewardId));
        if (reward == null) return;

        string resolvedCardId = !string.IsNullOrWhiteSpace(reward.cardId)
            ? reward.cardId
            : rewardId;

        if (!string.IsNullOrWhiteSpace(resolvedCardId))
            registry.AddItem(buyer, resolvedCardId, ItemType.TarotCard);

        // Keep shop-relevant outcomes immediate without requiring live player prefabs in bidding/shop scenes.
        if (reward.rewardType == TarotRewardType.Coins)
        {
            registry.AddGold(buyer, (int)reward.effectValue);
        }
        else if (reward.rewardType == TarotRewardType.Reroll)
        {
            GrantFreeRerollRpc(RpcTarget.Single(buyer, RpcTargetUse.Temp));
        }
    }

    //void ServerApplyLovers(ulong casterClientId)
    //{
    //    // Pick two random opponents (not the caster)
    //    var opponents = new System.Collections.Generic.List<ulong>();
    //    foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
    //        if (kvp.Key != casterClientId)
    //            opponents.Add(kvp.Key);

    //    if (opponents.Count < 2) return;

    //    int idxA = Random.Range(0, opponents.Count);
    //    int idxB;
    //    do { idxB = Random.Range(0, opponents.Count); } while (idxB == idxA);

    //    ulong partnerA = opponents[idxA];
    //    ulong partnerB = opponents[idxB];

    //    // Store the link on the caster's PlayerEffects so it can be read in combat
    //    var fx = PlayerEffects.GetEffects(casterClientId);
    //    if (fx != null)
    //    {
    //        fx.LoversPartnerA.Value = partnerA;
    //        fx.LoversPartnerB.Value = partnerB;
    //    }

    //    GameLogger.Log(LogSeverity.Debug, $"The Lovers: {partnerA} and {partnerB} now share health.");
    //}

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
        _hideTooltipCts?.Cancel();
        _hideTooltipCts?.Dispose();
        _hideTooltipCts = null;

        potOverlay?.SetActive(false);
        ClearCards();
        DestroyTooltip();
        _selectedCards.Clear();
        _autoResolvingSelection = false;
        _sequenceRunning = false;
    }

    public override void OnDestroy()
    {
        UnhookEvents();

        _hideTooltipCts?.Cancel();
        _hideTooltipCts?.Dispose();

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Tooltip

    private TarotCardReward _activeTooltipReward;

    private CancellationTokenSource _hideTooltipCts;

    /// <summary>Creates the tooltip once and reuses it — avoids flash from destroy/recreate.</summary>
    void EnsureTooltip()
    {
        if (_activeTooltip != null) return;
        if (tooltipPrefab == null) return;

        // Re-find canvas here in case it was null during Initialize
        if (_rootCanvas == null)
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            // Walk up to root
            Canvas c = _rootCanvas;
            while (c != null && !c.isRootCanvas)
            {
                Canvas parent = c.transform.parent?.GetComponentInParent<Canvas>();
                if (parent == null) break;
                c = parent;
            }
            _rootCanvas = c;
        }

        // Last resort — find any root canvas in the scene
        if (_rootCanvas == null)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.isRootCanvas) { _rootCanvas = c; break; }
            }
        }

        if (_rootCanvas == null) { GameLogger.Log(LogSeverity.Error, "Cannot find any Canvas!"); return; }

        var go = Instantiate(tooltipPrefab, _rootCanvas.transform);
        go.SetActive(false);
        _activeTooltip = go.GetComponent<CardTooltip>();
        if (_activeTooltip != null) _activeTooltip.SetCanvas(_rootCanvas);
    }

    private async UniTaskVoid ShowTooltipNextFrameAsync(RectTransform cardRect, float extraOffsetX = 20f)
    {
        await UniTask.Yield();
        if (this == null || _activeTooltip == null || cardRect == null) return;

        _activeTooltip.PositionBesideCard(cardRect, extraOffsetX);
        _activeTooltip.gameObject.SetActive(true);
    }

    void OnCardHover(TarotCardReward reward, bool enter)
    {
        if (enter)
        {
            // Cancel any pending hide if we're re-entering quickly
            _hideTooltipCts?.Cancel();
            _hideTooltipCts?.Dispose();
            _hideTooltipCts = null;

            if (_activeTooltip != null && _activeTooltipReward == reward)
                return;

            if (tooltipPrefab == null || _rootCanvas == null) return;

            DestroyTooltip();
            var go = Instantiate(tooltipPrefab, _rootCanvas.transform);
            _activeTooltip = go.GetComponent<CardTooltip>();
            _activeTooltipReward = reward;
            if (_activeTooltip == null) return;

            _activeTooltip.SetCanvas(_rootCanvas);

            if (_activeTooltip.nameText) _activeTooltip.nameText.text = reward.cardName;
            if (_activeTooltip.effectText) _activeTooltip.effectText.text = reward.rewardSummary;
            if (_activeTooltip.descText) _activeTooltip.descText.text = reward.flavorText;
            if (_activeTooltip.costText) _activeTooltip.costText.text = "";
            if (_activeTooltip.stockText) _activeTooltip.stockText.text = "";

            var hoveredCard = _spawnedCards.Find(c => c != null && c.Reward == reward);
            if (hoveredCard != null)
                _activeTooltip.PositionBesideCard(hoveredCard.GetComponent<RectTransform>());

            go.SetActive(true);
        }
        else
        {
            // Delay the actual destroy slightly — if a matching enter fires
            // within this window (edge jitter), it cancels this and nothing flashes.
            _hideTooltipCts?.Cancel();
            _hideTooltipCts?.Dispose();
            _hideTooltipCts = new CancellationTokenSource();
            HideTooltipAfterDelay(_hideTooltipCts.Token).Forget();
        }
    }

    void OnCardHoverExit()
    {
        if (_activeTooltip == null) return;

        _hideTooltipCts?.Cancel();
        _hideTooltipCts?.Dispose();
        _hideTooltipCts = new CancellationTokenSource();
        HideTooltipAfterDelay(_hideTooltipCts.Token).Forget();
    }

    private async UniTaskVoid HideTooltipAfterDelay(CancellationToken token)
    {
        bool cancelled = await UniTask.Delay(50, cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return; // a new hover-enter cancelled this before it fired

        if (this == null) return; // guard against destroy mid-wait

        DestroyTooltip();
        _activeTooltipReward = null;
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

    public PotType GetPotTypeBySize(PotSize size) => potUILookup[size]?.PotType;
    public TarotPotUIBehaviour GetPotUIBySize(PotSize size) => potUILookup[size];


    #endregion
}