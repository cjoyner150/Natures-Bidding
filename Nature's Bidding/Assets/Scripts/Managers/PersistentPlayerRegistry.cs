using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public enum ItemType { Mask, TarotCard, Artifact }

public class PersistentPlayerRegistry : Singleton<PersistentPlayerRegistry>
{
    private Dictionary<string, PersistentPlayerData> _playerData = new();
    private bool[] _indexPool = new bool[4];
    private Dictionary<ulong, string> _clientToAuth = new();

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    #region Registration

    public PersistentPlayerData RegisterPlayer(ulong clientId, string authId, string playerName)
    {
        if (!IsServer) return null;

        _clientToAuth[clientId] = authId;

        if (_playerData.TryGetValue(authId, out var existing))
        {
            existing.clientId = clientId;
            Debug.Log($"Returning player {playerName} rejoined with index {existing.playerIndex}");
            PlayerRegistryNetworkSync.Instance?.BroadcastPlayerData(
                clientId, authId, playerName, existing.playerIndex, existing.gold, existing.combatWins);
            return existing;
        }

        int index = AssignIndex();
        if (index == -1)
        {
            Debug.LogError($"[PersistentPlayerRegistry] No available player indices for {playerName} (authId: {authId}). Current pool state: {string.Join(",", _indexPool)}");
            return null;
        }

        var data = new PersistentPlayerData
        {
            clientId = clientId,
            authenticationId = authId,
            playerName = playerName,
            playerIndex = index,
            gold = Random.Range(80, 120),
            combatWins = 0,
            masks = { "butterfly_mask" },
            tarotCards = { "the_star_tarot" },
            artifacts = { "move_speed_up", "sprinter" }
        };

        _playerData[authId] = data;
        Debug.Log($"New player {playerName} registered with index {index}");

        PlayerRegistryNetworkSync.Instance?.BroadcastPlayerData(clientId, authId, playerName, index, data.gold, 0);

        foreach (var mask in data.masks)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(clientId, mask, ItemType.Mask);
        foreach (var artifact in data.artifacts)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(clientId, artifact, ItemType.Artifact);
        foreach (var tarot in data.tarotCards)
            PlayerRegistryNetworkSync.Instance?.BroadcastItem(clientId, tarot, ItemType.TarotCard);

        return data;
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
        Debug.Log($"Player {authId} disconnected during gameplay — data retained.");
    }

    public bool TryReconnectPlayer(ulong newClientId, string authId, out PersistentPlayerData data)
    {
        if (!IsServer) { data = null; return false; }

        if (_playerData.TryGetValue(authId, out data))
        {
            data.clientId = newClientId;
            _clientToAuth[newClientId] = authId;
            Debug.Log($"Player {authId} reconnected as clientId {newClientId}");
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

    public PersistentPlayerData GetByClientId(ulong clientId)
    {
        if (_clientToAuth.TryGetValue(clientId, out var authId))
            if (_playerData.TryGetValue(authId, out var data))
                return data;
        return null;
    }

    public PersistentPlayerData GetByAuthId(string authId)
    {
        _playerData.TryGetValue(authId, out var data);
        return data;
    }

    public bool HasPlayer(ulong clientId) => _clientToAuth.ContainsKey(clientId);

    public List<PersistentPlayerData> GetAllPlayers() => new(_playerData.Values);

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

    #endregion

    #region Apply (called by network sync on clients)

    public void ApplyPlayerData(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins)
    {
        _clientToAuth[clientId] = authId;

        if (!_playerData.ContainsKey(authId))
        {
            _playerData[authId] = new PersistentPlayerData
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

    private void ApplyItemLocal(PersistentPlayerData data, string itemId, ItemType type)
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

    public IEnumerable<PersistentPlayerData> GetSnapshot() => _playerData.Values;

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