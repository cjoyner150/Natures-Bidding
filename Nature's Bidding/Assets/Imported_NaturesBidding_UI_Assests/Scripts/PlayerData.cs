using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayerData — One NetworkObject per connected player.
/// All stats are NetworkVariables so every client can read them for the shop panel.
/// </summary>
public class PlayerData : NetworkBehaviour
{
    #region Static Registry

    private static readonly Dictionary<ulong, PlayerData> _registry = new Dictionary<ulong, PlayerData>();

    public static PlayerData GetPlayer(ulong clientId)
    {
        // Try registry first (fast path)
        if (_registry.TryGetValue(clientId, out var p) && p != null)
            return p;

        // Fallback — search all active PlayerData objects in the scene
        // This handles the timing gap where the shop starts before OnNetworkSpawn fires
        foreach (var pd in Object.FindObjectsByType<PlayerData>(FindObjectsSortMode.None))
        {
            if (pd.OwnerClientId == clientId)
            {
                _registry[clientId] = pd;   // Re-register so future lookups are fast
                return pd;
            }
        }

        return null;
    }

    /// <summary>Returns all registered players.</summary>
    public static IEnumerable<PlayerData> GetAllPlayers()
    {
        // Merge registry with scene search so nothing is missed
        var found = new Dictionary<ulong, PlayerData>(_registry);
        foreach (var pd in Object.FindObjectsByType<PlayerData>(FindObjectsSortMode.None))
            if (!found.ContainsKey(pd.OwnerClientId))
                found[pd.OwnerClientId] = pd;
        return found.Values;
    }

    #endregion

    #region Network Variables — synced to all clients

    public NetworkVariable<int> Coins = new NetworkVariable<int>(
        1000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<NetworkString> PlayerName = new NetworkVariable<NetworkString>(
        new NetworkString("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Stats as NetworkVariables so the shop panel on every client can read them
    public NetworkVariable<float> SpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> JumpMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> DamageMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> DefenseMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> MaxHealthBonus = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Server-Side State (not synced — server only)

    public List<string> Items { get; private set; } = new List<string>();
    public Dictionary<string, int> UpgradeCounts { get; private set; } = new Dictionary<string, int>();

    #endregion

    #region Lifecycle

    public override void OnNetworkSpawn()
    {
        _registry[OwnerClientId] = this;
        Debug.Log($"[PlayerData] Registered client {OwnerClientId} — total in registry: {_registry.Count}");

        if (IsOwner)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}");
            SetNameRpc(savedName);
        }
    }

    public override void OnNetworkDespawn()
    {
        _registry.Remove(OwnerClientId);
    }

    #endregion

    #region Server Methods

    public void AddItemServerSide(string itemName)
    {
        if (!IsServer) return;
        Items.Add(itemName);
        Debug.Log($"[Server] Player {OwnerClientId} received item: {itemName}");
    }

    public void AddUpgradeServerSide(string upgradeName, float effectValue, UpgradeType upgradeType)
    {
        if (!IsServer) return;

        if (!UpgradeCounts.ContainsKey(upgradeName)) UpgradeCounts[upgradeName] = 0;
        UpgradeCounts[upgradeName]++;

        // Writing to NetworkVariables — automatically synced to all clients
        switch (upgradeType)
        {
            case UpgradeType.SpeedPercent:   SpeedMultiplier.Value   += effectValue; break;
            case UpgradeType.JumpPercent:    JumpMultiplier.Value    += effectValue; break;
            case UpgradeType.DamagePercent:  DamageMultiplier.Value  += effectValue; break;
            case UpgradeType.DefensePercent: DefenseMultiplier.Value += effectValue; break;
            case UpgradeType.HealthPercent:  MaxHealthBonus.Value    += effectValue; break;
            case UpgradeType.CoinBonus:      AddCoins((int)effectValue);             break;
        }
    }

    public bool SpendCoins(int amount)
    {
        if (!IsServer || Coins.Value < amount) return false;
        Coins.Value -= amount;
        return true;
    }

    public void AddCoins(int amount)
    {
        if (!IsServer) return;
        Coins.Value += amount;
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.Server)]
    void SetNameRpc(string name)
    {
        PlayerName.Value = new NetworkString(name);
    }

    #endregion

    #region Stand-In Visual

    [Header("Stand-in Visual")]
    public Renderer standInRenderer;
    public TMP_Text standInLabel;

    void Update()
    {
        if (standInRenderer != null && IsOwner)
            standInRenderer.material.color = Color.green;

        if (standInLabel != null)
            standInLabel.text = PlayerName.Value.Value;
    }

    #endregion
}