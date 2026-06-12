using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<StatusEffectorSO> _allStatusEffectors;

    private Dictionary<string, StatusEffectorSO> _lookup;

    public void Initialize()
    {
        _lookup = new Dictionary<string, StatusEffectorSO>();
        foreach (var effector in _allStatusEffectors)
        {
            if (string.IsNullOrEmpty(effector.Id))
            {
                Debug.LogError($"Effector {effector.name} has no ID — skipping.");
                continue;
            }
            if (_lookup.ContainsKey(effector.Id))
            {
                Debug.LogError($"Duplicate ID: {effector.Id} on {effector.name}");
                continue;
            }
            _lookup[effector.Id] = effector;
        }
    }

    public StatusEffectorSO Get(string id)
    {
        if (_lookup.TryGetValue(id, out var effector)) return effector;
        Debug.LogError($"ItemDatabase: no effector found for id '{id}'");
        return null;
    }

    public bool TryGet(string id, out StatusEffectorSO effector) =>
        _lookup.TryGetValue(id, out effector);
}