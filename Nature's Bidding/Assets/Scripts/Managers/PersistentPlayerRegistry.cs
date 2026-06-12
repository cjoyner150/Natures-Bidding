using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public enum ItemType { Mask, TarotCard, Artifact }

public class PersistentPlayerRegistry : NetworkSingleton<PersistentPlayerRegistry>
{
    private Dictionary<string, PersistentPlayerData> _playerData = new();
    private bool[] _indexPool = new bool[4];
    private Dictionary<ulong, string> _clientToAuth = new();

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

            BroadcastPlayerDataRpc(clientId, authId, playerName, existing.playerIndex, existing.gold, existing.combatWins);
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
            gold = 100,
            combatWins = 0,

            artifacts =
            {
                "hp_up",
                "damage_up",
                "dash_distance_up",
                "fast_hands",
                "absorber",
                "snake_mask"
            }
        };

        _playerData[authId] = data;
        Debug.Log($"New player {playerName} registered with index {index}");

        BroadcastPlayerDataRpc(clientId, authId, playerName, index, 0, 0);
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
        BroadcastUnregisterRpc(clientId, authId);
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
            BroadcastPlayerDataRpc(newClientId, authId, data.playerName, data.playerIndex, data.gold, data.combatWins);
            return true;
        }

        data = null;
        return false;
    }

    public void SyncAllToClient(ulong targetClientId)
    {
        if (!IsServer) return;

        foreach (var data in _playerData.Values)
        {
            SendPlayerDataToClientRpc(
                data.clientId, data.authenticationId, data.playerName,
                data.playerIndex, data.gold, data.combatWins,
                NetworkManager.Singleton.RpcTarget.Single(targetClientId, RpcTargetUse.Temp)
            );

            foreach (var mask in data.masks)
                SendItemToClientRpc(data.clientId, mask, ItemType.Mask,
                    NetworkManager.Singleton.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            foreach (var card in data.tarotCards)
                SendItemToClientRpc(data.clientId, card, ItemType.TarotCard,
                    NetworkManager.Singleton.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
            foreach (var artifact in data.artifacts)
                SendItemToClientRpc(data.clientId, artifact, ItemType.Artifact,
                    NetworkManager.Singleton.RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
        }
    }

    public void Clear()
    {
        if (!IsServer) return;
        _playerData.Clear();
        _clientToAuth.Clear();
        _indexPool = new bool[4];
        BroadcastClearRpc();
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

    public List<PersistentPlayerData> GetAllPlayers() =>
        new List<PersistentPlayerData>(_playerData.Values);

    #endregion

    #region Economy

    public bool TrySpendGold(ulong clientId, int amount)
    {
        if (!IsServer) return false;
        var data = GetByClientId(clientId);
        if (data == null || data.gold < amount) return false;
        data.gold -= amount;
        Debug.Log($"[{GetType().Name}] client {data.playerName} gold has been set to {data.gold} by server.");
        BroadcastGoldRpc(clientId, data.gold);
        return true;
    }

    public void AddGold(ulong clientId, int amount)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;
        data.gold += amount;
        Debug.Log($"[{GetType().Name}] client {data.playerName} gold has been set to {data.gold} by server.");
        BroadcastGoldRpc(clientId, data.gold);
    }

    public void AddCombatWin(ulong clientId)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;
        data.combatWins++;
        BroadcastCombatWinsRpc(clientId, data.combatWins);
    }

    public void AddItem(ulong clientId, string itemId, ItemType type)
    {
        if (!IsServer) return;
        var data = GetByClientId(clientId);
        if (data == null) return;

        switch (type)
        {
            case ItemType.Mask: data.masks.Add(itemId); break;
            case ItemType.TarotCard: data.tarotCards.Add(itemId); break;
            case ItemType.Artifact: data.artifacts.Add(itemId); break;
        }

        BroadcastItemRpc(clientId, itemId, type);
    }

    #endregion

    #region RPCs

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastPlayerDataRpc(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins)
    {
        ApplyPlayerData(clientId, authId, playerName, playerIndex, gold, combatWins);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendPlayerDataToClientRpc(ulong clientId, string authId, string playerName,
        int playerIndex, int gold, int combatWins, RpcParams rpcParams = default)
    {
        ApplyPlayerData(clientId, authId, playerName, playerIndex, gold, combatWins);
    }

    private void ApplyPlayerData(ulong clientId, string authId, string playerName,
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

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastGoldRpc(ulong clientId, int gold)
    {
        var data = GetByClientId(clientId);
        if (data == null)
        {
            Debug.LogWarning($"BroadcastGoldRpc: no entry for {clientId}");
            return;
        }
        data.gold = gold;

        Debug.Log($"[{GetType().Name}] client {data.playerName} gold has been set to {data.gold} by server.");
    }

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastCombatWinsRpc(ulong clientId, int wins)
    {
        var data = GetByClientId(clientId);
        if (data != null) data.combatWins = wins;
    }

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastItemRpc(ulong clientId, string itemId, ItemType type)
    {
        var data = GetByClientId(clientId);
        if (data == null) return;
        ApplyItem(data, itemId, type);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendItemToClientRpc(ulong clientId, string itemId, ItemType type,
        RpcParams rpcParams = default)
    {
        var data = GetByClientId(clientId);
        if (data == null) return;
        ApplyItem(data, itemId, type);
    }

    private void ApplyItem(PersistentPlayerData data, string itemId, ItemType type)
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

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastUnregisterRpc(ulong clientId, string authId)
    {
        _playerData.Remove(authId);
        _clientToAuth.Remove(clientId);
    }

    [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
    private void BroadcastClearRpc()
    {
        _playerData.Clear();
        _clientToAuth.Clear();
        _indexPool = new bool[4];
    }

    #endregion

    #region Index Pool

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

    #endregion
}