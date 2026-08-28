using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public enum ItemType { Mask, TarotCard, Artifact }

public class PersistentPlayerRegistry : Singleton<PersistentPlayerRegistry>
{
    [SerializeField] PlayerStartConfigSO playerStartConfig;

    private Dictionary<string, PlayerData> _playerData = new();
    private bool[] _indexPool = new bool[4];
    private Dictionary<ulong, string> _clientToAuth = new();

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    #region Registration

    public PlayerData RegisterPlayer(ulong clientId, string authId, string playerName)
    {
        if (!IsServer) return null;

        _clientToAuth[clientId] = authId;

        if (_playerData.TryGetValue(authId, out var existing))
        {
            bool oldClientStillConnected =
                existing.clientId != clientId &&
                NetworkManager.Singleton.ConnectedClients.ContainsKey(existing.clientId);

            if (!oldClientStillConnected)
            {
                _clientToAuth.Remove(existing.clientId); // clean up the stale mapping
                existing.clientId = clientId;
                GameLogger.Log(LogSeverity.Debug, $"Returning player {playerName} rejoined with index {existing.playerIndex}");
                PlayerRegistryNetworkSync.Instance?.BroadcastPlayerData(
                    clientId, authId, playerName, existing.playerIndex, existing.gold, existing.combatWins);
                return existing;
            }

            GameLogger.Log(LogSeverity.Warning, $"Duplicate authId '{authId}' while original client {existing.clientId} is still connected — registering as a NEW player.");
            authId = $"{authId}_dup{clientId}"; // give the concurrent duplicate its own identity
            _clientToAuth[clientId] = authId;
        }

        int index = AssignIndex();
        if (index == -1)
        {
            GameLogger.Log(LogSeverity.Error, $"No available player indices for {playerName} (authId: {authId}). Current pool state: {string.Join(",", _indexPool)}");
            return null;
        }

        var data = new PlayerData
        {
            clientId = clientId,
            authenticationId = authId,
            playerName = playerName,
            playerIndex = index,
            gold = playerStartConfig.gold,
            combatWins = 0,
            speedMultiplier = 1f,
            jumpMultiplier = 1f,
            damageMultiplier = 1f,
            defenseMultiplier = 1f,
            maxHealthBonus = 0f,
            items = new List<string>(),
            upgradeCounts = new Dictionary<string, int>(),
            masks = playerStartConfig.masks.Select(m => m.Id).ToList(),
            tarotCards = playerStartConfig.tarots.Select(t => t.Id).ToList(),
            artifacts = playerStartConfig.artifacts.Select(a => a.Id).ToList()
        };

        _playerData[authId] = data;
        GameLogger.Log(LogSeverity.Debug, $"New player {playerName} registered with index {index}");
        BroadcastAll(data);

        return data;
    }

    private void BroadcastAll(PlayerData data)
    {
        GameLogger.Log(LogSeverity.Debug, $"Broadcasting player data for client {data.clientId}, {data.playerName} at index {data.playerIndex}: \n" +
            "Masks: " + string.Join(", ", data.masks) + "\n" +
            "Tarots: " + string.Join(", ", data.tarotCards) + "\n" +
            "Artifacts: " + string.Join(", ", data.artifacts));

        PlayerRegistryNetworkSync.Instance?.BroadcastPlayerData(data.clientId, data.authenticationId, data.playerName, data.playerIndex, data.gold, 0);

        foreach (var mask in data.masks)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(data.clientId, mask, ItemType.Mask);
        foreach (var artifact in data.artifacts)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(data.clientId, artifact, ItemType.Artifact);
        foreach (var tarot in data.tarotCards)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(data.clientId, tarot, ItemType.TarotCard);
    }

    public void UnregisterLobbyPlayer(ulong clientId)
    {
        if (!IsServer) return;
        if (!_clientToAuth.TryGetValue(clientId, out var authId)) return;

        if (_playerData.TryGetValue(authId, out var data))
        {
            FreeIndex(data.playerIndex);
            _playerData.Remove(authId);
        }

        _clientToAuth.Remove(clientId);
        PlayerRegistryNetworkSync.Instance?.BroadcastUnregister(clientId, authId);
    }

    public void MarkPlayerDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!_clientToAuth.TryGetValue(clientId, out var authId)) return;
        _clientToAuth.Remove(clientId);
        GameLogger.Log(LogSeverity.Info, $"Player {authId} disconnected during gameplay — data retained.");
    }

    public bool TryReconnectPlayer(ulong newClientId, string authId, out PlayerData data)
    {
        if (!IsServer) { data = null; return false; }

        if (_playerData.TryGetValue(authId, out data))
        {
            data.clientId = newClientId;
            _clientToAuth[newClientId] = authId;
            GameLogger.Log(LogSeverity.Info, $"Player {authId} reconnected as clientId {newClientId}");
            PlayerRegistryNetworkSync.Instance?.BroadcastPlayerData(
                newClientId, authId, data.playerName, data.playerIndex, data.gold, data.combatWins);
            return true;
        }

        data = null;
        return false;
    }

    public void Clear()
    {
        if (!IsServer) return;
        _playerData.Clear();
        _clientToAuth.Clear();
        _indexPool = new bool[4];
        PlayerRegistryNetworkSync.Instance?.BroadcastClear();
    }

    #endregion

    #region Queries

    public PlayerData GetByClientId(ulong clientId)
    {
        if (_clientToAuth.TryGetValue(clientId, out var authId))
            if (_playerData.TryGetValue(authId, out var data))
                return data;
        return null;
    }

    public PlayerData GetByAuthId(string authId)
    {
        _playerData.TryGetValue(authId, out var data);
        return data;
    }

    public bool HasPlayer(ulong clientId) => _clientToAuth.ContainsKey(clientId);

    public List<PlayerData> GetAllPlayers() => new(_playerData.Values);

    #endregion

    #region Economy

    public bool TrySpendGold(ulong clientId, int amount)
    {
        if (!IsServer) return false;
        var data = GetByClientId(clientId);
        if (data == null || data.gold < amount) return false;
        data.gold -= amount;
        PlayerRegistryNetworkSync.Instance?.BroadcastGold(clientId, data.gold);
        return true;
    }

    public void AddGold(ulong clientId, int amount)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;
        data.gold += amount;
        PlayerRegistryNetworkSync.Instance?.BroadcastGold(clientId, data.gold);
    }

    public void AddCombatWin(ulong clientId)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;
        data.combatWins++;
        PlayerRegistryNetworkSync.Instance?.BroadcastCombatWins(clientId, data.combatWins);
    }

    public void AddItem(ulong clientId, string itemId, ItemType type)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;
        ApplyItemLocal(data, itemId, type);
        PlayerRegistryNetworkSync.Instance?.BroadcastItem(clientId, itemId, type);
    }

    public void SyncLivePlayerState(ulong clientId, string playerName, int gold, float speedMultiplier,
        float jumpMultiplier, float damageMultiplier, float defenseMultiplier, float maxHealthBonus,
        List<string> items, Dictionary<string, int> upgradeCounts)
    {
        if (!IsServer) return;

        var data = GetByClientId(clientId);
        if (data == null) return;

        data.clientId = clientId;
        if (!string.IsNullOrWhiteSpace(playerName))
            data.playerName = playerName;

        data.gold = gold;
        data.speedMultiplier = speedMultiplier;
        data.jumpMultiplier = jumpMultiplier;
        data.damageMultiplier = damageMultiplier;
        data.defenseMultiplier = defenseMultiplier;
        data.maxHealthBonus = maxHealthBonus;

        data.items = items != null ? new List<string>(items) : new List<string>();
        data.upgradeCounts = upgradeCounts != null ? new Dictionary<string, int>(upgradeCounts) : new Dictionary<string, int>();

        BroadcastAll(data);
    }

    #endregion

    #region Apply (called by network sync on clients)

    public void ApplyPlayerData(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins)
    {
        _clientToAuth[clientId] = authId;

        if (!_playerData.ContainsKey(authId))
        {
            _playerData[authId] = new PlayerData
            {
                clientId = clientId,
                authenticationId = authId,
                playerName = playerName,
                playerIndex = playerIndex,
                gold = gold,
                combatWins = combatWins
            };
        }
        else
        {
            var data = _playerData[authId];
            data.clientId = clientId;
            data.playerName = playerName;
            data.playerIndex = playerIndex;
            data.gold = gold;
            data.combatWins = combatWins;
        }
    }

    public void ApplyGold(ulong clientId, int gold)
    {
        var data = GetByClientId(clientId);
        if (data != null) data.gold = gold;
    }

    public void ApplyCombatWins(ulong clientId, int wins)
    {
        var data = GetByClientId(clientId);
        if (data != null) data.combatWins = wins;
    }

    public void ApplyItem(ulong clientId, string itemId, ItemType type)
    {
        var data = GetByClientId(clientId);
        if (data == null) return;
        ApplyItemLocal(data, itemId, type);
    }

    public void ApplyUnregister(ulong clientId, string authId)
    {
        _playerData.Remove(authId);
        _clientToAuth.Remove(clientId);
    }

    public void ApplyClear()
    {
        _playerData.Clear();
        _clientToAuth.Clear();
        _indexPool = new bool[4];
    }

    private void ApplyItemLocal(PlayerData data, string itemId, ItemType type)
    {
        switch (type)
        {
            case ItemType.Mask:
                if (!data.masks.Contains(itemId)) data.masks.Add(itemId); break;
            case ItemType.TarotCard:
                if (!data.tarotCards.Contains(itemId)) data.tarotCards.Add(itemId); break;
            case ItemType.Artifact:
                if (!data.artifacts.Contains(itemId)) data.artifacts.Add(itemId); break;
        }
    }

    public IEnumerable<PlayerData> GetSnapshot() => _playerData.Values;

    #endregion

    #region Index Pool

    private int AssignIndex()
    {
        for (int i = 0; i < _indexPool.Length; i++)
            if (!_indexPool[i]) { _indexPool[i] = true; return i; }
        return -1;
    }

    private void FreeIndex(int index)
    {
        if (index >= 0 && index < _indexPool.Length)
            _indexPool[index] = false;
    }

    #endregion
}