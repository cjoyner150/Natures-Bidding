using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PlayerShoppingNetworkBehavior — One NetworkObject per connected player.
/// This is the live networked player state used everywhere PlayerData used to be.
/// </summary>
public class PlayerShoppingNetworkBehavior : NetworkBehaviour
{
    #region Starting Values

    [Header("Starting Values")]
    [SerializeField] private int startingCoins = 1000;

    #endregion

    #region Static Registry

    private static readonly Dictionary<ulong, PlayerShoppingNetworkBehavior> _registry = new Dictionary<ulong, PlayerShoppingNetworkBehavior>();

    public static PlayerShoppingNetworkBehavior GetPlayer(ulong clientId)
    {
        if (_registry.TryGetValue(clientId, out var player) && player != null)
            return player;

        foreach (var data in UnityEngine.Object.FindObjectsByType<PlayerShoppingNetworkBehavior>(FindObjectsSortMode.None))
        {
            if (data.OwnerClientId == clientId)
            {
                _registry[clientId] = data;
                return data;
            }
        }

        return null;
    }

    public static IEnumerable<PlayerShoppingNetworkBehavior> GetAllPlayers()
    {
        var found = new Dictionary<ulong, PlayerShoppingNetworkBehavior>(_registry);
        foreach (var data in UnityEngine.Object.FindObjectsByType<PlayerShoppingNetworkBehavior>(FindObjectsSortMode.None))
            if (!found.ContainsKey(data.OwnerClientId))
                found[data.OwnerClientId] = data;
        return found.Values;
    }

    #endregion

    #region Network Variables

    public NetworkVariable<int> Coins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<NetworkString> PlayerName = new NetworkVariable<NetworkString>(
        new NetworkString("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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

    #region Server-Side State

    public List<string> Items { get; private set; } = new List<string>();
    public Dictionary<string, int> UpgradeCounts { get; private set; } = new Dictionary<string, int>();

    #endregion

    #region Lifecycle

    public override void OnNetworkSpawn()
    {
        _registry[OwnerClientId] = this;
        GameLogger.Log(LogSeverity.Debug, $"[PlayerShoppingNetworkBehavior] Registered client {OwnerClientId} — total in registry: {_registry.Count}");

        if (IsServer)
            LoadRuntimeData();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            SyncRegistrySnapshot();

        _registry.Remove(OwnerClientId);
    }

    void OnApplicationQuit()
    {
        if (IsServer)
            SyncRegistrySnapshot();
    }

    #endregion

    #region Server Methods

    public void AddItemServerSide(string itemName)
    {
        if (!IsServer) return;
        Items.Add(itemName);
        GameLogger.Log(LogSeverity.Debug, $"[Server] Player {OwnerClientId} received item: {itemName}");
        SyncRegistrySnapshot();
    }

    public void AddUpgradeServerSide(string upgradeId, float effectValue, UpgradeType upgradeType)
    {
        if (!IsServer) return;

        if (!UpgradeCounts.ContainsKey(upgradeId)) UpgradeCounts[upgradeId] = 0;
        UpgradeCounts[upgradeId]++;

        ApplyUpgradeEffect(upgradeType, effectValue);
        SyncRegistrySnapshot();
    }

    public bool SpendCoins(int amount)
    {
        if (!IsServer || Coins.Value < amount) return false;
        Coins.Value -= amount;
        SyncRegistrySnapshot();
        return true;
    }

    public void AddCoins(int amount)
    {
        if (!IsServer) return;
        Coins.Value += amount;
        SyncRegistrySnapshot();
    }

    public void AdjustStatMultiplier(UpgradeType upgradeType, float effectValue)
    {
        if (!IsServer) return;
        ApplyUpgradeEffect(upgradeType, effectValue);
        SyncRegistrySnapshot();
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.Server)]
    void SetNameRpc(string name)
    {
        PlayerName.Value = new NetworkString(name);
        SyncRegistrySnapshot();
    }

    #endregion

    #region Persistence

    private void LoadRuntimeData()
    {
        var registryData = PersistentPlayerRegistry.Instance?.GetByClientId(OwnerClientId);
        if (registryData == null)
        {
            ApplyDefaultState(GetRuntimePlayerName());
            SyncRegistrySnapshot();
            return;
        }

        ApplyRegistryState(registryData);
        SyncRegistrySnapshot();
    }

    private void ApplyDefaultState(string runtimeName)
    {
        Coins.Value = Mathf.Max(0, startingCoins);
        PlayerName.Value = new NetworkString(GetResolvedName(runtimeName, null));
        SpeedMultiplier.Value = 1f;
        JumpMultiplier.Value = 1f;
        DamageMultiplier.Value = 1f;
        DefenseMultiplier.Value = 1f;
        MaxHealthBonus.Value = 0f;
        Items.Clear();
        UpgradeCounts.Clear();
    }

    private void ApplyRegistryState(PlayerData registryState)
    {
        Coins.Value = Mathf.Max(0, registryState.gold);
        PlayerName.Value = new NetworkString(GetResolvedName(registryState.playerName, registryState.playerName));
        SpeedMultiplier.Value = registryState.speedMultiplier;
        JumpMultiplier.Value = registryState.jumpMultiplier;
        DamageMultiplier.Value = registryState.damageMultiplier;
        DefenseMultiplier.Value = registryState.defenseMultiplier;
        MaxHealthBonus.Value = registryState.maxHealthBonus;

        Items.Clear();
        if (registryState.items != null)
            Items.AddRange(registryState.items);

        UpgradeCounts.Clear();
        if (registryState.upgradeCounts != null)
            foreach (var upgrade in registryState.upgradeCounts)
                UpgradeCounts[upgrade.Key] = Mathf.Max(0, upgrade.Value);
    }

    private void SyncRegistrySnapshot()
    {
        if (!IsServer) return;

        var registry = PersistentPlayerRegistry.Instance;
        if (registry == null) return;

        var state = registry.GetByClientId(OwnerClientId);
        if (state == null)
        {
            return;
        }
        registry.SyncLivePlayerState(
            OwnerClientId,
            PlayerName.Value.Value,
            Coins.Value,
            SpeedMultiplier.Value,
            JumpMultiplier.Value,
            DamageMultiplier.Value,
            DefenseMultiplier.Value,
            MaxHealthBonus.Value,
            Items,
            UpgradeCounts);
    }

    private string GetRuntimePlayerName()
    {
        var registryData = PersistentPlayerRegistry.Instance?.GetByClientId(OwnerClientId);
        if (registryData != null && !string.IsNullOrWhiteSpace(registryData.playerName))
            return registryData.playerName;

        return PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}");
    }

    private static string GetResolvedName(string runtimeName, string savedName)
    {
        if (!string.IsNullOrWhiteSpace(runtimeName))
            return runtimeName;

        if (!string.IsNullOrWhiteSpace(savedName))
            return savedName;

        return "Player";
    }

    private void ApplyUpgradeEffect(UpgradeType upgradeType, float effectValue)
    {
        switch (upgradeType)
        {
            case UpgradeType.SpeedPercent:   SpeedMultiplier.Value   += effectValue; break;
            case UpgradeType.JumpPercent:    JumpMultiplier.Value    += effectValue; break;
            case UpgradeType.DamagePercent:  DamageMultiplier.Value  += effectValue; break;
            case UpgradeType.DefensePercent: DefenseMultiplier.Value += effectValue; break;
            case UpgradeType.HealthPercent:  MaxHealthBonus.Value    += effectValue; break;
            case UpgradeType.CoinBonus:      AddCoins((int)effectValue);             break;
        }

        SyncRegistrySnapshot();
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