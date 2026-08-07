using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;

/// <summary>
/// BiddingManager — Simultaneous reverse auction.
///
/// Rules:
///   • Every player bids every round, nobody sits out.
///   • Total rounds = number of connected players.
///   • All bids are hidden until everyone has submitted.
///   • Once the last bid is in, all are revealed at once.
///   • Lowest unique bid wins the item for that round.
///   • Ties: the lowest tied bidder is chosen at random.
///   • After all rounds are done, transition to shop automatically.
///   • Bid amount is controlled through keyboard input.
/// </summary>
public class BiddingManager : BaseGameServerHandler<BiddingManager>
{
    #region Inspector Fields

    [Header("2D HUD")]
    [SerializeField] private GameObject serializedBiddingCanvas;
    [SerializeField] private GameObject serializedShopCanvas;
    public GameObject     bidHUDPanel;
    public RectTransform   bidDisplayCard;     // Card/sprite root to flip when the bid changes
    public TMP_Text       bidAmountDisplay;   // Number shown on the card/sprite
    public TMP_Text       statusText;
    public TMP_Text       timerText;
    public TMP_Text       goldText;
    public TMP_Text       roundCounterText;   // "Round 2 / 4"
    public TMP_Text       waitingCountText;   // "2 / 4 bids submitted"

    [Header("Results Panel")]
    public GameObject resultsPanel;
    public Transform  resultsContainer;
    public GameObject resultRowPrefab;

    [Header("Settings")]
    [SerializeField] InputAction bidUpAction;
    [SerializeField] InputAction bidDownAction;
    [SerializeField] InputAction bidSubmitAction;
    public float roundTimerSeconds  = 30f;   // Safety timer in case a player disconnects
    public float resultsDisplayTime = 4f;
    public float bidFlipDuration    = 0.18f; // How long the flip animation lasts
    public float bidFlatScaleY      = 0.02f; // How flat the card gets mid-flip
    public int   bidStep            = 5;     // How much each key press changes the bid
    public int   minBid             = 0;
    public int   startingBid        = 0;

    [Header("Audio")]
    [SerializeField] private BiddingAudioFeedback audioFeedback;

    #endregion

    #region Network Variables

    public NetworkVariable<int> TimeRemaining = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> BidsReceived = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> TotalRounds = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Private State

    // Server-only
    private Dictionary<ulong, int> _bids           = new Dictionary<ulong, int>();
    private List<ulong>            _allPlayers      = new List<ulong>();
    private Coroutine              _timerCoroutine;
    private Coroutine              _bidFlipCoroutine;
    private bool                   _roundActive     = false;

    // Client-only
    private bool _localBidSubmitted = false;
    private int  _localBidAmount;

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (audioFeedback == null)
            audioFeedback = GetComponent<BiddingAudioFeedback>();
    }

    private void OnEnable()
    {
        bidUpAction.Enable();
        bidDownAction.Enable();
        bidSubmitAction.Enable();

        bidUpAction.performed += OnBidUp;
        bidDownAction.performed += OnBidDown;
        bidSubmitAction.performed += OnSubmitBid;
    }

    private void OnDisable()
    {
        bidUpAction.Disable();
        bidDownAction.Disable();
        bidSubmitAction.Disable();

        bidUpAction.performed -= OnBidUp;
        bidDownAction.performed -= OnBidDown;
        bidSubmitAction.performed -= OnSubmitBid;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        var flowManager = PersistentGameStateManager.Instance;
        if (flowManager != null)
        {
            var biddingCanvas = serializedBiddingCanvas;
            var shopCanvas = serializedShopCanvas;
            var shopManager = ShopManager.Instance != null ? ShopManager.Instance : FindAnyObjectByType<ShopManager>();
            var readyManager = ReadyManager.Instance != null ? ReadyManager.Instance : FindAnyObjectByType<ReadyManager>();

            flowManager.ConfigureGameFlowReferences(biddingCanvas, shopCanvas, this, shopManager, readyManager);
            flowManager.OnBiddingSceneReady();

            if (IsServer)
                flowManager.InitializeBiddingFlowIfServer().Forget();
        }

        TimeRemaining.OnValueChanged += (_, t) =>
        {
            if (timerText) timerText.text = $"{t}s";
        };

        BidsReceived.OnValueChanged += (_, received) =>
        {
            if (waitingCountText)
                waitingCountText.text = $"{received} / {TotalPlayers.Value} bids submitted";
        };

        CurrentRound.OnValueChanged += (_, round) => RefreshRoundCounter();
        TotalRounds.OnValueChanged  += (_, _total)  => RefreshRoundCounter();

        if (bidHUDPanel != null) bidHUDPanel.SetActive(false);
        if (resultsPanel != null) resultsPanel.SetActive(false);

        InitializeGoldAsync();
    }

    public async void OnBiddingPhaseStart()
    {
        PointerNPC.Instance?.CelebrateOne();
        PointerNPC.Instance?.SayOpeningInstructions();
    }

    private async void InitializeGoldAsync()
    {
        var localPlayer = PersistentPlayerRegistry.Instance.GetByClientId(NetworkManager.LocalClientId);

        if (localPlayer == null)
        {
            await UniTask.WaitUntil(() => PersistentPlayerRegistry.Instance.GetByClientId(NetworkManager.LocalClientId) != null);
            localPlayer = PersistentPlayerRegistry.Instance.GetByClientId(NetworkManager.LocalClientId);
        }

        goldText.text = localPlayer.gold.ToString();
    }

    #endregion

    #region Entry Points

    /// <summary>Called by the server flow handler at the start of bidding.</summary>
    public void BeginBiddingPhase()
    {
        if (!IsServer) return;

        // Build the permanent player list from whoever is connected.
        _allPlayers.Clear();
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
            _allPlayers.Add(kvp.Key);

        int playerCount      = _allPlayers.Count;
        TotalPlayers.Value   = playerCount;
        TotalRounds.Value    = playerCount;   // One round per player
        CurrentRound.Value   = 0;

        StartCoroutine(BeginRoundsWhenPlayersReady());
    }

    [Rpc(SendTo.Server)]
    public void StartBiddingPhaseRpc() => BeginBiddingPhase();

    public void OnPlayerDeath(ulong clientId) { }

    #endregion

    #region Round Loop

    IEnumerator RunAllRounds()
    {
        if (!IsServer) yield break;

        for (int round = 0; round < TotalRounds.Value; round++)
        {
            CurrentRound.Value = round + 1;
            yield return StartCoroutine(RunSingleRound());
        }

        // All rounds complete
        ShowTransitionMessageRpc("Bidding over! Heading to the shop...");
        yield return new WaitForSeconds(2f);
        PersistentGameStateManager.Instance?.RequestStartShopPhase();
    }

    IEnumerator RunSingleRound()
    {
        if (!IsServer) yield break;

        _bids.Clear();
        _roundActive       = true;
        BidsReceived.Value = 0;

        // Pick item and notify clients to reset UI and show bid panel
        BiddingArenaManager.Instance?.PickNextItem();
        ResetUIRpc();

        // Start safety timer (in case a player's connection drops mid-round)
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(RoundTimer());

        // Block until every player has bid OR timer expires
        yield return new WaitUntil(() => !_roundActive);

        // Auto-fill skipped players with int.MaxValue (worst possible bid)
        foreach (var id in _allPlayers)
            if (!_bids.ContainsKey(id))
                _bids[id] = int.MaxValue;

        BiddingArenaManager.Instance?.ClearItemDisplay();

        // Determine winner
        ulong winnerId = DetermineWinner();
        var currentItem = BiddingArenaManager.Instance?.GetCurrentItem();
        string itemId = currentItem?.itemId ?? "item";
        string itemName = currentItem?.itemName ?? "Item";

        if (winnerId != ulong.MaxValue)
            PersistentPlayerRegistry.Instance?.AddItem(winnerId, itemId, ItemType.Mask);

        // Pack and send results
        var sb    = new System.Text.StringBuilder();
        bool first = true;
        foreach (var kvp in _bids)
        {
            if (!first) sb.Append(',');
            first = false;
            int display = kvp.Value == int.MaxValue ? -1 : kvp.Value;
            sb.Append(kvp.Key).Append(':').Append(display);
        }

        bool tie = winnerId == ulong.MaxValue;
        RevealResultsRpc(
            sb.ToString(),
            winnerId.ToString(),
            tie ? 0 : _bids[winnerId],
            tie ? "" : GetPlayerName(winnerId),
            itemName);

        yield return new WaitForSeconds(resultsDisplayTime);
    }

    IEnumerator BeginRoundsWhenPlayersReady()
    {
        int waitedFrames = 0;

        while (true)
        {
            bool allReady = true;
            var registry = PersistentPlayerRegistry.Instance;
            if (registry == null)
            {
                allReady = false;
            }
            else if (registry.GetAllPlayers().Count < _allPlayers.Count)
            {
                allReady = false;
            }
            else
            {
                foreach (ulong clientId in _allPlayers)
                {
                    if (registry.GetByClientId(clientId) == null)
                    {
                        allReady = false;
                        break;
                    }
                }
            }

            if (allReady)
                break;

            waitedFrames++;
            if (waitedFrames % 300 == 0)
                Debug.LogWarning($"[BiddingManager] Waiting for persistent player registry... frame {waitedFrames}");

            yield return null;
        }

        BiddingArenaManager.Instance?.AssignPlayersToSeats();
        StartCoroutine(RunAllRounds());
    }

    IEnumerator RoundTimer()
    {
        int t = (int)roundTimerSeconds;
        while (t > 0 && _roundActive)
        {
            TimeRemaining.Value = t;
            yield return new WaitForSeconds(1f);
            t--;
        }
        TimeRemaining.Value = 0;
        _roundActive = false;   // Forces WaitUntil to exit
    }

    #endregion

    #region Winner Logic

    ulong DetermineWinner()
    {
        List<ulong> tiedLowestBidders = new List<ulong>();
        int lowestBid = int.MaxValue;

        foreach (var kvp in _bids)
        {
            if (kvp.Value == int.MaxValue) continue;   // Skipped
            if (kvp.Value < lowestBid)
            {
                lowestBid = kvp.Value;
                tiedLowestBidders.Clear();
                tiedLowestBidders.Add(kvp.Key);
            }
            else if (kvp.Value == lowestBid)
            {
                tiedLowestBidders.Add(kvp.Key);
            }
        }

        if (tiedLowestBidders.Count == 0)
            return ulong.MaxValue;

        return tiedLowestBidders[Random.Range(0, tiedLowestBidders.Count)];
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.Everyone)]
    void ResetUIRpc()
    {
        _localBidSubmitted  = false;
        _localBidAmount     = startingBid;

        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (bidHUDPanel != null) bidHUDPanel.SetActive(true);           // Everyone sees the bid panel simultaneously

        if (statusText)      statusText.text      = "Place your bid — Lowest wins!";
        if (waitingCountText) waitingCountText.text = $"0 / {TotalPlayers.Value} bids submitted";

        RefreshBidDisplay(false);
        RefreshSubmitButton();
    }

    [Rpc(SendTo.Everyone)]
    void RevealResultsRpc(string packedBids, string winnerIdStr, int winningBid, string winnerName, string itemName)
    {
        if (resultsPanel != null) resultsPanel.SetActive(true);

        PointerNPC.Instance?.CelebrateTwo();
        PointerNPC.Instance?.SayBiddingFinished();
        if (winnerIdStr == ulong.MaxValue.ToString())
            PointerNPC.Instance?.SayNoWinner(itemName);
        else
            PointerNPC.Instance?.SayWinner(winnerName, itemName);

        if (resultsContainer != null)
        {
            foreach (Transform child in resultsContainer) Destroy(child.gameObject);
        }

        if (resultsContainer != null && resultRowPrefab != null)
        {
            foreach (string entry in packedBids.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;

                ulong pid    = ulong.Parse(parts[0]);
                int amount   = int.Parse(parts[1]);
                bool winner  = parts[0] == winnerIdStr;
                bool skipped = amount == -1;

                var row   = Instantiate(resultRowPrefab, resultsContainer);
                var label = row.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = $"{GetPlayerName(pid)}: {(skipped ? "[SKIP] Timed out" : amount + " coins")} {(winner ? "[WIN] Wins!" : "")}";
            }
        }

        bool tie = winnerIdStr == ulong.MaxValue.ToString();
        if (statusText)
            statusText.text = tie
                ? "TIE — No winner this round!"
                : $"{winnerName} wins with {winningBid} coins!";
    }

    [Rpc(SendTo.Everyone)]
    void ShowTransitionMessageRpc(string message)
    {
        if (resultsPanel != null) resultsPanel.SetActive(false);
        PointerNPC.Instance?.SetIdle();
        PointerNPC.Instance?.SayTransition();
        if (statusText) statusText.text = message;
    }

    #endregion

    #region Player Input — Bid Controls

    /// <summary>Increase bid by one step. Call from + button or right trigger.</summary>
    public void OnBidUp(InputAction.CallbackContext callbackContext)
    {
        if (!bidHUDPanel || !bidHUDPanel.activeInHierarchy) return;
        if (_localBidSubmitted) return;

        Debug.Log($"[BiddingManager] OnBidUp: _localBidAmount = {_localBidAmount} and bidStep = {bidStep}");
        int nextBid = _localBidAmount + bidStep;
        if (nextBid == _localBidAmount)
            return;

        if (TryGetLocalPlayerGold(out int availableGold) && nextBid > availableGold)
        {
            audioFeedback?.PlayBidReject();
            return;
        }

        Debug.Log($"[BiddingManager] gold = {availableGold}");

        _localBidAmount = nextBid;
        RefreshBidDisplay(true);
        audioFeedback?.PlayBidUp();
    }

    /// <summary>Decrease bid by one step. Call from - button or left trigger.</summary>
    public void OnBidDown(InputAction.CallbackContext callbackContext)
    {
        if (!bidHUDPanel || !bidHUDPanel.activeInHierarchy) return;
        if (_localBidSubmitted) return;

        Debug.Log($"[BiddingManager] OnBidDown: _localBidAmount = {_localBidAmount} and bidStep = {bidStep}");
        int nextBid = _localBidAmount - bidStep;
        if (nextBid == _localBidAmount)
            return;
        Debug.Log($"[BiddingManager] OnBidDown: nextBid = {nextBid} and minBid = {minBid}");
        if (nextBid < minBid)
        {
            audioFeedback?.PlayBidReject();
            return;
        }

        _localBidAmount = nextBid;
        RefreshBidDisplay(true);
        audioFeedback?.PlayBidDown();
    }

    /// <summary>Submit the current bid. Call from Submit button or confirm button.</summary>
    public void OnSubmitBid(InputAction.CallbackContext callbackContext)
    {
        if (!bidHUDPanel || !bidHUDPanel.activeInHierarchy) return;
        if (_localBidSubmitted) return;

        if (_localBidAmount < minBid ||
            (TryGetLocalPlayerGold(out int availableGold) && _localBidAmount > availableGold))
        {
            audioFeedback?.PlayBidReject();
            return;
        }

        goldText.text = (availableGold - _localBidAmount).ToString();
        _localBidSubmitted = true;
        audioFeedback?.PlayBidSubmit();
        RefreshSubmitButton();
        SetBidDisplayVisible(false);
        if (statusText) statusText.text = $"Bid of {_localBidAmount} submitted! Waiting for others...";

        SubmitBidRpc(_localBidAmount);
    }

    [Rpc(SendTo.Server)]
    void SubmitBidRpc(int amount, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (!_roundActive)             return;
        if (_bids.ContainsKey(sender)) return;
        if (amount < minBid)           return;

        // Validate the player has enough coins
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry?.GetByClientId(sender);
        if (player == null)
        {
            Debug.LogWarning($"[BiddingManager] Bid rejected for client {sender}: persistent registry data not ready.");
            BidRejectedRpc("Persistent player data not ready yet.", RpcTarget.Single(sender, RpcTargetUse.Temp));
            return;
        }

        if (player.gold < amount)
        {
            BidRejectedRpc("Not enough coins!", RpcTarget.Single(sender, RpcTargetUse.Temp));
            return;
        }

        // Deduct coins immediately on bid submission
        if (!registry.TrySpendGold(sender, amount))
        {
            BidRejectedRpc("Not enough coins!", RpcTarget.Single(sender, RpcTargetUse.Temp));
            return;
        }

        _bids[sender]      = amount;
        BidsReceived.Value = _bids.Count;

        // All expected players have bid — end the round early
        if (_bids.Count >= _allPlayers.Count)
            _roundActive = false;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void BidRejectedRpc(string reason, RpcParams rpcParams = default)
    {
        // Reset so the player can adjust and try again
        _localBidSubmitted = false;
        audioFeedback?.PlayBidReject();
        RefreshSubmitButton();
        if (statusText)     statusText.text = $"Bid rejected: {reason}";
        if (bidHUDPanel != null) bidHUDPanel.SetActive(true);
        SetBidDisplayVisible(true);
    }

    void RefreshBidDisplay(bool animate)
    {
        if (bidAmountDisplay)
            bidAmountDisplay.text = _localBidAmount.ToString();

        SetBidDisplayVisible(true);

        if (animate)
            RestartBidFlipAnimation();
        else
            ResetBidFlipVisual();
    }

    void RefreshSubmitButton()
    {
        
    }

    RectTransform GetBidFlipTarget()
    {
        if (bidDisplayCard) return bidDisplayCard;
        if (bidAmountDisplay) return bidAmountDisplay.rectTransform;
        return null;
    }

    void SetBidDisplayVisible(bool visible)
    {
        if (bidDisplayCard != null)
            bidDisplayCard.gameObject.SetActive(visible);

        if (bidAmountDisplay != null)
            bidAmountDisplay.gameObject.SetActive(visible);
    }

    void ResetBidFlipVisual()
    {
        RectTransform target = GetBidFlipTarget();
        if (!target) return;

        Vector3 scale = target.localScale;
        target.localScale = new Vector3(scale.x, 1f, scale.z);
    }

    void RestartBidFlipAnimation()
    {
        RectTransform target = GetBidFlipTarget();
        if (!target) return;

        if (_bidFlipCoroutine != null)
            StopCoroutine(_bidFlipCoroutine);

        _bidFlipCoroutine = StartCoroutine(PlayBidFlipAnimation(target));
    }

    IEnumerator PlayBidFlipAnimation(RectTransform target)
    {
        Vector3 startScale = target.localScale;
        Vector3 flatScale   = new Vector3(startScale.x, bidFlatScaleY, startScale.z);
        Vector3 fullScale   = new Vector3(startScale.x, 1f, startScale.z);

        target.localScale = flatScale;

        float halfDuration = Mathf.Max(0.01f, bidFlipDuration * 0.5f);
        float elapsed      = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            target.localScale = Vector3.Lerp(flatScale, fullScale, t);
            yield return null;
        }

        target.localScale = fullScale;
        _bidFlipCoroutine = null;
    }

    void RefreshRoundCounter()
    {
        if (roundCounterText)
            roundCounterText.text = $"Round {CurrentRound.Value} / {TotalRounds.Value}";
    }

    #endregion

    #region Helpers

    string GetPlayerName(ulong clientId)
    {
        var p = PersistentPlayerRegistry.Instance?.GetByClientId(clientId);
        return p != null ? p.playerName : $"Player {clientId}";
    }

    bool TryGetLocalPlayerGold(out int gold)
    {
        gold = 0;

        if (NetworkManager.Singleton == null || PersistentPlayerRegistry.Instance == null)
            return false;

        var player = PersistentPlayerRegistry.Instance.GetByClientId(NetworkManager.Singleton.LocalClientId);
        if (player == null)
            return false;

        gold = player.gold;
        return true;
    }

    #endregion
}
