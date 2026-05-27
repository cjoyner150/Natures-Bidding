using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ShopManager — Networked split-screen shop.
///
/// Screen layout:
///   2×2 grid of PlayerShopPanel prefabs, one per player.
///   Each player sees their own quarter as interactive, the other three as read-only.
///
/// Per-player shop:
///   • Server rolls 3 random upgrades independently for each player.
///   • Offerings are packed and broadcast to all clients so everyone sees everyone's shop.
///   • Reroll replaces that player's 3 upgrades with 3 new random ones.
///   • Pot card is always present in each panel (once per phase per player).
///
/// Purchase flow:
///   Click card → selects it, detail panel shows in that panel.
///   Click Buy  → upgrade: deduct coins, apply stat.
///              → pot: deduct coins, open full-screen PotManager sequence.
/// </summary>
public class ShopManager : NetworkBehaviour
{
    public static ShopManager Instance { get; private set; }

    public static int SmallPotCost => Instance?.smallPotCost ?? 20;
    public static int GrandPotCost => Instance?.grandPotCost ?? 50;
    public static int PotCost      => SmallPotCost; // legacy fallback

    #region Inspector Fields

    [Header("Upgrade Pool")]
    public List<ShopUpgrade> upgradePool = new List<ShopUpgrade>();

    [Header("Screen Layout")]
    public Transform  shopPanelsContainer;      // Grid Layout Group — holds up to 4 PlayerShopPanel prefabs
    public GameObject playerShopPanelPrefab;    // PlayerShopPanel prefab

    [Header("Shop Settings")]
    public int smallPotCost = 20;
    public int grandPotCost = 50;
    public int rerollCost   = 15;

    [Header("Navigation")]
    public Button   backToBiddingButton;        // Host only
    public TMP_Text phaseLabel;

    #endregion

    #region Private State

    // clientId → panel
    private Dictionary<ulong, PlayerShopPanel> _panels        = new Dictionary<ulong, PlayerShopPanel>();

    // clientId → their current 3 offerings (server-side source of truth)
    private Dictionary<ulong, List<ShopUpgrade>> _offerings   = new Dictionary<ulong, List<ShopUpgrade>>();

    // clientId → free rerolls granted (e.g. from tarot)
    private Dictionary<ulong, int> _freeRerolls               = new Dictionary<ulong, int>();

    #endregion

    #region Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        backToBiddingButton?.gameObject.SetActive(IsHost);
    }

    public void OnShopPhaseStart()
    {
        if (phaseLabel) phaseLabel.text = "Shop";
        PotManager.Instance?.ResetForNewPhase();

        if (IsServer)
            ServerRollAllOfferings();
    }

    public void PopulateShopsServerSide() { }

    #endregion

    #region Server — Roll Offerings

    /// <summary>
    /// Server rolls 3 random upgrades independently for every connected player,
    /// then broadcasts the full set to all clients so every panel can be rendered.
    /// </summary>
    void ServerRollAllOfferings()
    {
        _offerings.Clear();
        _freeRerolls.Clear();

        var packedSB = new System.Text.StringBuilder();
        bool firstPlayer = true;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong id = kvp.Key;
            var offerings = RollThree();
            _offerings[id] = offerings;
            _freeRerolls[id] = 0;

            if (!firstPlayer) packedSB.Append(';');
            firstPlayer = false;

            // Pack: "clientId:upgrade1|upgrade2|upgrade3"
            packedSB.Append(id).Append(':');
            packedSB.Append(PackOfferings(offerings));
        }

        SyncAllOfferingsRpc(packedSB.ToString());
    }

    List<ShopUpgrade> RollThree()
    {
        var pool   = new List<ShopUpgrade>(upgradePool);
        var result = new List<ShopUpgrade>();

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j   = Random.Range(0, i + 1);
            var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int take = Mathf.Min(6, pool.Count);
        for (int i = 0; i < take; i++)
            result.Add(pool[i]);

        return result;
    }

    string PackOfferings(List<ShopUpgrade> offerings)
    {
        var names = new List<string>();
        foreach (var u in offerings)
            names.Add(u != null ? u.name : "null");
        return string.Join("|", names);
    }

    List<ShopUpgrade> UnpackOfferings(string packed)
    {
        var result = new List<ShopUpgrade>();
        foreach (var name in packed.Split('|'))
        {
            var upgrade = upgradePool.Find(u => u != null && u.name == name);
            if (upgrade != null) result.Add(upgrade);
        }
        return result;
    }

    #endregion

    #region RPC — Sync All Offerings to All Clients

    /// <summary>
    /// Received by every client. Builds all 4 player panels using the server's rolled data.
    /// Format: "clientId:upg1|upg2|upg3;clientId:upg1|upg2|upg3;..."
    /// </summary>
    [Rpc(SendTo.Everyone)]
    void SyncAllOfferingsRpc(string packedAll)
    {
        foreach (var panel in _panels.Values)
            if (panel) Destroy(panel.gameObject);
        _panels.Clear();

        if (playerShopPanelPrefab == null)
        {
            Debug.LogError("[ShopManager] playerShopPanelPrefab is not assigned in the Inspector!");
            return;
        }
        if (shopPanelsContainer == null)
        {
            Debug.LogError("[ShopManager] shopPanelsContainer is not assigned in the Inspector!");
            return;
        }

        var playerEntries = packedAll.Split(';');
        foreach (var entry in playerEntries)
        {
            if (string.IsNullOrEmpty(entry)) continue;

            int colon = entry.IndexOf(':');
            if (colon < 0) continue;

            ulong clientId    = ulong.Parse(entry.Substring(0, colon));
            string packedOffs = entry.Substring(colon + 1);
            var offerings     = UnpackOfferings(packedOffs);
            bool isLocal      = clientId == NetworkManager.Singleton.LocalClientId;

            var go    = Instantiate(playerShopPanelPrefab, shopPanelsContainer);
            var panel = go.GetComponent<PlayerShopPanel>();

            if (panel == null)
            {
                Debug.LogError("[ShopManager] playerShopPanelPrefab is missing the PlayerShopPanel component!");
                continue;
            }

            Debug.Log($"[ShopManager] Building panel for client {clientId} isLocal:{isLocal} offerings:{offerings.Count}");
            panel.Initialise(clientId, offerings, isLocal);
            _panels[clientId] = panel;
        }
    }

    #endregion

    #region Purchase — Upgrade

    /// <summary>Called by the local player's panel when Buy is clicked on an upgrade.</summary>
    public void LocalPlayerBuyUpgrade(ShopUpgrade upgrade, PlayerShopPanel sourcePanel)
    {
        BuyUpgradeRpc(upgrade.name);
    }

    [Rpc(SendTo.Server)]
    void BuyUpgradeRpc(string upgradeName, RpcParams rpcParams = default)
    {
        ulong buyer  = rpcParams.Receive.SenderClientId;
        var player   = PlayerData.GetPlayer(buyer);
        if (player == null) return;

        var upgrade = upgradePool.Find(u => u != null && u.name == upgradeName);
        if (upgrade == null) return;

        if (player.Coins.Value < upgrade.cost)
        {
            PurchaseFailedRpc("Not enough coins!", RpcTarget.Single(buyer, RpcTargetUse.Temp));
            return;
        }

        player.SpendCoins(upgrade.cost);
        player.AddUpgradeServerSide(upgradeName, upgrade.effectValue, upgrade.upgradeType);

        // Tell all clients so every panel can refresh that player's stats
        UpgradePurchasedRpc(buyer, upgradeName);
    }

    /// <summary>Broadcast to everyone so all panels showing this player refresh.</summary>
    [Rpc(SendTo.Everyone)]
    void UpgradePurchasedRpc(ulong buyer, string upgradeName)
    {
        if (_panels.TryGetValue(buyer, out var panel))
            panel.OnUpgradePurchased(upgradeName);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void PurchaseFailedRpc(string reason, RpcParams rpcParams = default)
    {
        // Surface the failure in the local player's panel buy button
        ulong local = NetworkManager.Singleton.LocalClientId;
        if (_panels.TryGetValue(local, out var panel))
        {
            // Panel's buy button text is updated internally — just refresh it
            Debug.LogWarning($"[Shop] Purchase failed: {reason}");
        }
    }

    #endregion

    #region Purchase — Pot

    /// <summary>Called by the local player's panel when Buy is clicked on the pot card.</summary>
    public void LocalPlayerBuyPot(PlayerShopPanel sourcePanel, bool isGrand)
    {
        BuyPotRpc(isGrand);
    }

    [Rpc(SendTo.Server)]
    void BuyPotRpc(bool isGrand, RpcParams rpcParams = default)
    {
        ulong buyer  = rpcParams.Receive.SenderClientId;
        var player   = PlayerData.GetPlayer(buyer);
        if (player == null) return;

        int cost = isGrand ? grandPotCost : smallPotCost;
        if (player.Coins.Value < cost) return;

        player.SpendCoins(cost);
        PotUsedRpc(buyer, isGrand);
        OpenPotSequenceRpc(isGrand, RpcTarget.Single(buyer, RpcTargetUse.Temp));
    }

    /// <summary>Broadcast so all panels showing this player mark pot as used.</summary>
    [Rpc(SendTo.Everyone)]
    void PotUsedRpc(ulong buyer, bool isGrand)
    {
        if (_panels.TryGetValue(buyer, out var panel))
            panel.OnPotUsed(isGrand);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void OpenPotSequenceRpc(bool isGrand, RpcParams rpcParams = default)
    {
        PotManager.Instance?.OpenSequence(isGrand);
    }

    #endregion

    #region Reroll

    public void LocalPlayerReroll()
    {
        RerollRpc();
    }

    [Rpc(SendTo.Server)]
    void RerollRpc(RpcParams rpcParams = default)
    {
        ulong buyer = rpcParams.Receive.SenderClientId;

        // Check free rerolls first, then charge coins
        int freeCount = _freeRerolls.ContainsKey(buyer) ? _freeRerolls[buyer] : 0;
        if (freeCount > 0)
        {
            _freeRerolls[buyer]--;
        }
        else
        {
            var player = PlayerData.GetPlayer(buyer);
            if (player == null || player.Coins.Value < rerollCost) return;
            player.SpendCoins(rerollCost);
        }

        var newOfferings  = RollThree();
        _offerings[buyer] = newOfferings;

        // Send new offerings to everyone so all panels update
        RerollResultRpc(buyer, PackOfferings(newOfferings));
    }

    /// <summary>Broadcast so all panels showing this player refresh their cards.</summary>
    [Rpc(SendTo.Everyone)]
    void RerollResultRpc(ulong buyer, string packedOfferings)
    {
        var offerings = UnpackOfferings(packedOfferings);
        if (_panels.TryGetValue(buyer, out var panel))
            panel.ApplyNewOfferings(offerings);
    }

    public void GrantFreeReroll(ulong clientId)
    {
        if (!_freeRerolls.ContainsKey(clientId)) _freeRerolls[clientId] = 0;
        _freeRerolls[clientId]++;
    }

    #endregion

    #region Navigation

    public void OnBackToBidding() => GameFlowManager.Instance?.StartBiddingPhaseRpc();

    #endregion
}