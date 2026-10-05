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
///              → pot: deduct coins, open full-screen TarotPotManager sequence.
/// </summary>
public class ShopManager : BaseGameServerHandler<ShopManager>
{

    #region Inspector Fields

    [Header("Upgrade Pool")]
    public List<ShopUpgrade> upgradePool = new List<ShopUpgrade>();

    [Header("Screen Layout")]
    public Transform  shopPanelsContainer;      // Grid Layout Group — holds up to 4 PlayerShopPanel prefabs
    public GameObject playerShopPanelPrefab;    // PlayerShopPanel prefab

    [Header("Shop Settings")]
    public int rerollCost   = 15;

    [Header("Navigation")]
    public TMP_Text phaseLabel;

    [Header("Shop Visuals")]
    public GameObject shopCanvasRoot;
    public GameObject playerCrosshairPrefab;

    #endregion

    #region Private State

    // clientId → panel
    private Dictionary<ulong, PlayerShopPanel> _panels        = new Dictionary<ulong, PlayerShopPanel>();

    // clientId → their current 3 offerings (server-side source of truth)
    private Dictionary<ulong, List<ShopUpgrade>> _offerings   = new Dictionary<ulong, List<ShopUpgrade>>();

    // clientId → free rerolls granted (e.g. from tarot)
    private Dictionary<ulong, int> _freeRerolls               = new Dictionary<ulong, int>();

    private Dictionary<string, ShopUpgrade> _upgradeLookup    = new Dictionary<string, ShopUpgrade>();

    #endregion

    #region Lifecycle

    void Awake() { }

    public async override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        BuildUpgradeLookup();

        var flowManager = PersistentGameStateManager.Instance;
        if (flowManager != null)
        {
            var shopManager = ShopManager.Instance != null ? ShopManager.Instance : FindAnyObjectByType<ShopManager>();
            var readyManager = ReadyManager.Instance != null ? ReadyManager.Instance : FindAnyObjectByType<ReadyManager>();

            await SceneReadiness.WaitForAllPlayersLoaded();

            flowManager.ConfigureGameFlowReferences(null, this, readyManager);
            flowManager.OnShopSceneReady();

            if (IsServer)
                flowManager.BeginShopPhaseServer();
        }
    }

    public void OnShopPhaseStart()
    {
        GameLogger.Log(LogSeverity.Info, "Shop phase is starting...");

        if (phaseLabel) phaseLabel.text = "Shop Phase";
        SetShopBackgroundVisible(true);

        OnShopPhaseStartEveryoneRpc();
        
        SpawnAllPlayerCrosshairs();

        if (IsServer)
            ServerRollAllOfferings();
    }

    [Rpc(SendTo.Everyone)]
    public void OnShopPhaseStartEveryoneRpc()
    {
        // stubbed for now
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

    public void PopulateShopsServerSide() { }

    void SetShopBackgroundVisible(bool visible)
    {
        if (shopCanvasRoot != null && shopCanvasRoot.activeSelf != visible)
            shopCanvasRoot.SetActive(visible);
    }

    #endregion

    #region Server — Roll Offerings

    /// <summary>
    /// Server rolls 3 random upgrades independently for every connected player,
    /// then broadcasts the full set to all clients so every panel can be rendered.
    /// </summary>
    void ServerRollAllOfferings()
    {
        BuildUpgradeLookup();
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
            names.Add(u != null ? u.Id : "null");
        return string.Join("|", names);
    }

    List<ShopUpgrade> UnpackOfferings(string packed)
    {
        var result = new List<ShopUpgrade>();
        foreach (var id in packed.Split('|'))
        {
            if (_upgradeLookup.TryGetValue(id, out var upgrade))
                result.Add(upgrade);
        }
        return result;
    }

    void BuildUpgradeLookup()
    {
        _upgradeLookup.Clear();
        foreach (var upgrade in upgradePool)
        {
            if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Id))
                continue;

            if (_upgradeLookup.ContainsKey(upgrade.Id))
            {
                GameLogger.Log(LogSeverity.Warning, $"Duplicate upgrade ID '{upgrade.Id}' on '{upgrade.name}'.");
                continue;
            }

            _upgradeLookup[upgrade.Id] = upgrade;
        }
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
        if (phaseLabel) phaseLabel.text = "Shop Phase";

        SetShopBackgroundVisible(true);

        foreach (Transform child in shopPanelsContainer)
            if (child != null) Destroy(child.gameObject);
        _panels.Clear();

        if (playerShopPanelPrefab == null)
        {
            GameLogger.Log(LogSeverity.Error, "playerShopPanelPrefab is not assigned in the Inspector!");
            return;
        }
        if (shopPanelsContainer == null)
        {
            GameLogger.Log(LogSeverity.Error, "shopPanelsContainer is not assigned in the Inspector!");
            return;
        }

        var playerEntries = packedAll.Split(';');
        int createdPanels = 0;
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
                GameLogger.Log(LogSeverity.Error, "playerShopPanelPrefab is missing the PlayerShopPanel component!");
                continue;
            }

            GameLogger.Log(LogSeverity.Info, $"Building panel for client {clientId} isLocal:{isLocal} offerings:{offerings.Count}");
            panel.Initialize(clientId, offerings, isLocal);
            _panels[clientId] = panel;
            createdPanels++;
        }

        const int targetPanelCount = 4;
        while (createdPanels < targetPanelCount)
        {
            var go = Instantiate(playerShopPanelPrefab, shopPanelsContainer);
            var panel = go.GetComponent<PlayerShopPanel>();
            if (panel == null)
            {
                Destroy(go);
                break;
            }

            panel.InitializePlaceholder($"Open Slot {createdPanels + 1}", new List<ShopUpgrade>());
            createdPanels++;
        }
    }

    #endregion

    #region Purchase — Upgrade

    /// <summary>Called by the local player's panel when Buy is clicked on an upgrade.</summary>
    public void LocalPlayerBuyUpgrade(ShopUpgrade upgrade, PlayerShopPanel sourcePanel)
    {
        GameLogger.Log(LogSeverity.Info, $"LocalPlayerBuyUpgrade requested. id:{upgrade?.Id} localClient:{NetworkManager.Singleton?.LocalClientId}");
        BuyUpgradeRpc(upgrade.Id);
    }

    [Rpc(SendTo.Server)]
    void BuyUpgradeRpc(string upgradeId, RpcParams rpcParams = default)
    {
        ulong buyer  = rpcParams.Receive.SenderClientId;
        var registry = PersistentPlayerRegistry.Instance;
        var playerState = registry?.GetByClientId(buyer);
        if (registry == null || playerState == null) return;

        if (!_upgradeLookup.TryGetValue(upgradeId, out var upgrade))
            return;

        if (playerState.gold < upgrade.cost)
        {
            PurchaseFailedRpc("Not enough coins!", RpcTarget.Single(buyer, RpcTargetUse.Temp));
            return;
        }

        if (!registry.TrySpendGold(buyer, upgrade.cost))
        {
            PurchaseFailedRpc("Not enough coins!", RpcTarget.Single(buyer, RpcTargetUse.Temp));
            return;
        }

        if (!string.IsNullOrWhiteSpace(upgrade.effectorId))
            registry.AddItem(buyer, upgrade.effectorId, upgrade.effectorBucket);

        var player = PlayerShoppingNetworkBehavior.GetPlayer(buyer);
        if (player != null)
        {
            player.AddUpgradeServerSide(upgradeId, upgrade.effectValue, upgrade.upgradeType);
        }

        // Tell all clients so every panel can refresh that player's stats
        UpgradePurchasedRpc(buyer, upgradeId);
    }

    /// <summary>Broadcast to everyone so all panels showing this player refresh.</summary>
    [Rpc(SendTo.Everyone)]
    void UpgradePurchasedRpc(ulong buyer, string upgradeId)
    {
        if (_panels.TryGetValue(buyer, out var panel))
            panel.OnUpgradePurchased(upgradeId);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void PurchaseFailedRpc(string reason, RpcParams rpcParams = default)
    {
        // Surface the failure in the local player's panel buy button
        ulong local = NetworkManager.Singleton.LocalClientId;
        if (_panels.TryGetValue(local, out var panel))
        {
            // Panel's buy button text is updated internally — just refresh it
            GameLogger.Log(LogSeverity.Warning, $"Purchase failed: {reason}");
        }
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
            var registry = PersistentPlayerRegistry.Instance;
            var playerState = registry?.GetByClientId(buyer);
            if (registry == null || playerState == null || playerState.gold < rerollCost) return;
            if (!registry.TrySpendGold(buyer, rerollCost)) return;
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

    public void OnPlayerDeath(ulong clientId) { }

    #endregion
}