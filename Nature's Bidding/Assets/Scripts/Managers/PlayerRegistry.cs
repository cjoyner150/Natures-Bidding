using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public class PlayerRegistry : Singleton<PlayerRegistry>
{
    private Dictionary<ulong, PlayerServerInfo> _registry = new();

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    public void Register(ulong clientId, string authId, string playerName)
    {
        Debug.Log(playerName + " has been registered");
        _registry[clientId] = new PlayerServerInfo(clientId, authId, playerName);
    }

    public PlayerServerInfo Get(ulong clientId) =>
        _registry.TryGetValue(clientId, out var info) ? info : default;

    public IReadOnlyCollection<PlayerServerInfo> GetAll() => _registry.Values;

    public void Remove(ulong clientId) => _registry.Remove(clientId);

    public bool Has(ulong clientId) => _registry.ContainsKey(clientId);
}