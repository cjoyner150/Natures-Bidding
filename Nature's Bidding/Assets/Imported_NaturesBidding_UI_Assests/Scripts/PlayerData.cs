using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using TMPro;

/// <summary>
/// PlayerData — One NetworkObject per connected player.
/// All stats are NetworkVariables so every client can read them for the shop panel.
/// </summary>
public class PlayerData : NetworkBehaviour
{
    private const string SaveFolderName = "PlayerDataSaves";

    #region Starting Values

    [Header("Starting Values")]
    [SerializeField] private int startingCoins = 1000;

    #endregion

    #region Static Registry

    private static readonly Dictionary<ulong, PlayerData> _registry = new Dictionary<ulong, PlayerData>();

    public static PlayerData GetPlayer(ulong clientId)
    {
        // Try registry first (fast path)
        if (_registry.TryGetValue(clientId, out var p) && p != null)
            return p;

        // Fallback — search all active PlayerData objects in the scene
        // This handles the timing gap where the shop starts before OnNetworkSpawn fires
        foreach (var pd in UnityEngine.Object.FindObjectsByType<PlayerData>(FindObjectsSortMode.None))
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
        foreach (var pd in UnityEngine.Object.FindObjectsByType<PlayerData>(FindObjectsSortMode.None))
            if (!found.ContainsKey(pd.OwnerClientId))
                found[pd.OwnerClientId] = pd;
        return found.Values;
    }

    #endregion

    #region Network Variables — synced to all clients

    public NetworkVariable<int> Coins = new NetworkVariable<int>(
        0,
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

        if (IsServer)
            LoadPersistentState();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            SavePersistentState();

        _registry.Remove(OwnerClientId);
    }

    void OnApplicationQuit()
    {
        if (IsServer)
            SavePersistentState();
    }

    #endregion

    #region Server Methods

    public void AddItemServerSide(string itemName)
    {
        if (!IsServer) return;
        Items.Add(itemName);
        Debug.Log($"[Server] Player {OwnerClientId} received item: {itemName}");
        SavePersistentState();
    }

    public void AddUpgradeServerSide(string upgradeName, float effectValue, UpgradeType upgradeType)
    {
        if (!IsServer) return;

        if (!UpgradeCounts.ContainsKey(upgradeName)) UpgradeCounts[upgradeName] = 0;
        UpgradeCounts[upgradeName]++;

        ApplyUpgradeEffect(upgradeType, effectValue);
        SavePersistentState();
    }

    public bool SpendCoins(int amount)
    {
        if (!IsServer || Coins.Value < amount) return false;
        Coins.Value -= amount;
        SavePersistentState();
        return true;
    }

    public void AddCoins(int amount)
    {
        if (!IsServer) return;
        Coins.Value += amount;
        SavePersistentState();
    }

    public void AdjustStatMultiplier(UpgradeType upgradeType, float effectValue)
    {
        if (!IsServer) return;
        ApplyUpgradeEffect(upgradeType, effectValue);
        SavePersistentState();
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.Server)]
    void SetNameRpc(string name)
    {
        PlayerName.Value = new NetworkString(name);
        SavePersistentState();
    }

    #endregion

    #region Persistence

    [SerializeField] private bool enablePersistentSave = true;

    private string _persistenceKey;

    private void LoadPersistentState()
    {
        if (!enablePersistentSave)
        {
            ApplyDefaultState(GetRuntimePlayerName());
            return;
        }

        string runtimeName = GetRuntimePlayerName();
        string persistenceKey = GetPersistenceKey();

        if (string.IsNullOrWhiteSpace(persistenceKey))
        {
            ApplyDefaultState(runtimeName);
            return;
        }

        PlayerSaveData saveData = ReadSaveData(persistenceKey);
        if (saveData == null)
        {
            ApplyDefaultState(runtimeName);
            SavePersistentState();
            return;
        }

        ApplySaveData(saveData, runtimeName);
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

    private void ApplySaveData(PlayerSaveData saveData, string runtimeName)
    {
        Coins.Value = Mathf.Max(0, saveData.coins);
        PlayerName.Value = new NetworkString(GetResolvedName(runtimeName, saveData.playerName));
        SpeedMultiplier.Value = saveData.speedMultiplier;
        JumpMultiplier.Value = saveData.jumpMultiplier;
        DamageMultiplier.Value = saveData.damageMultiplier;
        DefenseMultiplier.Value = saveData.defenseMultiplier;
        MaxHealthBonus.Value = saveData.maxHealthBonus;

        Items.Clear();
        if (saveData.items != null)
            Items.AddRange(saveData.items);

        UpgradeCounts.Clear();
        if (saveData.upgradeCounts != null)
        {
            foreach (var upgrade in saveData.upgradeCounts)
            {
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.upgradeName))
                    continue;

                UpgradeCounts[upgrade.upgradeName] = Mathf.Max(0, upgrade.count);
            }
        }
    }

    private void SavePersistentState()
    {
        if (!IsServer || !enablePersistentSave) return;

        string persistenceKey = GetPersistenceKey();
        if (string.IsNullOrWhiteSpace(persistenceKey)) return;

        try
        {
            Directory.CreateDirectory(GetSaveDirectory());
            File.WriteAllText(GetSavePath(persistenceKey), JsonUtility.ToJson(CaptureSaveData(), true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PlayerData] Failed to save data for {OwnerClientId}: {exception.Message}");
        }
    }

    private PlayerSaveData CaptureSaveData()
    {
        var saveData = new PlayerSaveData
        {
            playerName = PlayerName.Value.Value,
            coins = Coins.Value,
            speedMultiplier = SpeedMultiplier.Value,
            jumpMultiplier = JumpMultiplier.Value,
            damageMultiplier = DamageMultiplier.Value,
            defenseMultiplier = DefenseMultiplier.Value,
            maxHealthBonus = MaxHealthBonus.Value,
            items = new List<string>(Items)
        };

        foreach (var upgrade in UpgradeCounts)
        {
            saveData.upgradeCounts.Add(new UpgradeCountSaveData
            {
                upgradeName = upgrade.Key,
                count = upgrade.Value
            });
        }

        return saveData;
    }

    private string GetPersistenceKey()
    {
        if (!string.IsNullOrWhiteSpace(_persistenceKey))
            return _persistenceKey;

        var registryData = PersistentPlayerRegistry.Instance?.GetByClientId(OwnerClientId);
        if (registryData != null && !string.IsNullOrWhiteSpace(registryData.authenticationId))
        {
            _persistenceKey = registryData.authenticationId;
            return _persistenceKey;
        }

        if (!string.IsNullOrWhiteSpace(AuthenticationService.Instance?.PlayerId))
        {
            _persistenceKey = AuthenticationService.Instance.PlayerId;
            return _persistenceKey;
        }

        return null;
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

    private static PlayerSaveData ReadSaveData(string persistenceKey)
    {
        string path = GetSavePath(persistenceKey);
        if (!File.Exists(path))
            return null;

        return JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
    }

    private static string GetSaveDirectory() => Path.Combine(Application.persistentDataPath, SaveFolderName);

    private static string GetSavePath(string persistenceKey) => Path.Combine(GetSaveDirectory(), $"{SanitizeFileName(persistenceKey)}.json");

    private static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        char[] fileName = value.ToCharArray();

        for (int i = 0; i < fileName.Length; i++)
        {
            if (Array.IndexOf(invalidCharacters, fileName[i]) >= 0)
                fileName[i] = '_';
        }

        return new string(fileName);
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
    }

    [Serializable]
    private class PlayerSaveData
    {
        public string playerName = "Player";
        public int coins = 0;
        public float speedMultiplier = 1f;
        public float jumpMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float defenseMultiplier = 1f;
        public float maxHealthBonus = 0f;
        public List<string> items = new List<string>();
        public List<UpgradeCountSaveData> upgradeCounts = new List<UpgradeCountSaveData>();
    }

    [Serializable]
    private class UpgradeCountSaveData
    {
        public string upgradeName;
        public int count;
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