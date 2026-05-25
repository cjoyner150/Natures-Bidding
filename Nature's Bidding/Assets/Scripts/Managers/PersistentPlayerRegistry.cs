using System.Collections.Generic;
using UnityEngine;
using UnityUtils;

public class PersistentPlayerRegistry : Singleton<PersistentPlayerRegistry>
{
    private Dictionary<string, PersistentPlayerData> _playerData = new();
    private bool[] _indexPool = new bool[4];
    private Dictionary<ulong, string> _clientToAuth = new();

    protected override void Awake()
    {
        if (HasInstance) Destroy(gameObject);
        else
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }
    }

    public PersistentPlayerData RegisterPlayer(ulong clientId, string authId, string playerName)
    {
        _clientToAuth[clientId] = authId;

        if (_playerData.TryGetValue(authId, out var existing))
        {
            existing.clientId = clientId;
            Debug.Log($"Returning player {playerName} rejoined with index {existing.playerIndex}");
            return existing;
        }

        int index = AssignIndex();
        if (index == -1)
        {
            Debug.LogError("No available player indices.");
            return null;
        }

        var data = new PersistentPlayerData
        {
            clientId = clientId,
            authenticationId = authId,
            playerName = playerName,
            playerIndex = index,
            gold = 0,
            combatWins = 0
        };

        _playerData[authId] = data;
        Debug.Log($"New player {playerName} registered with index {index}");
        return data;
    }

    public void UnregisterLobbyPlayer(ulong clientId)
    {
        if (!_clientToAuth.TryGetValue(clientId, out var authId)) return;

        if (_playerData.TryGetValue(authId, out var data))
        {
            FreeIndex(data.playerIndex);
            _playerData.Remove(authId);
        }

        _clientToAuth.Remove(clientId);
    }

    public void MarkPlayerDisconnected(ulong clientId)
    {
        if (!_clientToAuth.TryGetValue(clientId, out var authId)) return;
        _clientToAuth.Remove(clientId);
        Debug.Log($"Player {authId} disconnected during gameplay — data retained.");
    }

    public bool TryReconnectPlayer(ulong newClientId, string authId, out PersistentPlayerData data)
    {
        if (_playerData.TryGetValue(authId, out data))
        {
            data.clientId = newClientId;
            _clientToAuth[newClientId] = authId;
            Debug.Log($"Player {authId} reconnected as clientId {newClientId}");
            return true;
        }
        data = null;
        return false;
    }

    public PersistentPlayerData GetByClientId(ulong clientId)
    {
        if (_clientToAuth.TryGetValue(clientId, out var authId))
        {
            if (_playerData.TryGetValue(authId, out var data)) return data;
        }

        return null;
    }

    public PersistentPlayerData GetByAuthId(string authId)
    {
        _playerData.TryGetValue(authId, out var data);
        return data;
    }

    public bool HasPlayer(ulong clientId) => _clientToAuth.ContainsKey(clientId);

    public List<PersistentPlayerData> GetAllPlayers() =>
        new List<PersistentPlayerData>(_playerData.Values);

    public void Clear()
    {
        _playerData.Clear();
        _clientToAuth.Clear();
        _indexPool = new bool[4];
    }

    private int AssignIndex()
    {
        for (int i = 0; i < _indexPool.Length; i++)
        {
            if (!_indexPool[i])
            {
                _indexPool[i] = true;
                return i;
            }
        }
        return -1;
    }

    private void FreeIndex(int index)
    {
        if (index >= 0 && index < _indexPool.Length)
            _indexPool[index] = false;
    }

    public bool TrySpendGold(ulong clientId, int amount)
    {
        var data = GetByClientId(clientId);
        if (data == null || data.gold < amount) return false;
        data.gold -= amount;
        return true;
    }

    public void AddGold(ulong clientId, int amount)
    {
        var data = GetByClientId(clientId);
        if (data != null) data.gold += amount;
    }

    public void AddCombatWin(ulong clientId)
    {
        var data = GetByClientId(clientId);
        if (data != null) data.combatWins++;
    }

    public void AddItem(ulong clientId, string itemId, ItemType type)
    {
        var data = GetByClientId(clientId);
        if (data == null) return;

        switch (type)
        {
            case ItemType.Mask: data.masks.Add(itemId); break;
            case ItemType.TarotCard: data.tarotCards.Add(itemId); break;
            case ItemType.Artifact: data.artifacts.Add(itemId); break;
        }
    }
}

public enum ItemType { Mask, TarotCard, Artifact }