using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<StatusEffectorSO> allStatusEffectors;
    [SerializeField] private List<WeaponConfigSO> allWeapons;
    [SerializeField] private List<MaskVisualSO> allMaskVisuals;

    private Dictionary<string, StatusEffectorSO> statusLookup;
    private Dictionary<string, WeaponConfigSO> weaponLookup;
    private Dictionary<string, MaskVisualSO> maskVisualLookup;

    public void Initialize()
    {
        statusLookup = new Dictionary<string, StatusEffectorSO>();
        foreach (var effector in allStatusEffectors)
        {
            if (string.IsNullOrEmpty(effector.Id))
            {
                Debug.LogError($"Effector {effector.name} has no ID - skipping.");
                continue;
            }
            if (statusLookup.ContainsKey(effector.Id))
            {
                Debug.LogError($"Duplicate ID: {effector.Id} on {effector.name}");
                continue;
            }
            statusLookup[effector.Id] = effector;
        }

        weaponLookup = new Dictionary<string, WeaponConfigSO>();
        foreach (var weapon in allWeapons)
        {
            if (string.IsNullOrEmpty(weapon.Id))
            {
                Debug.LogError($"Effector {weapon.name} has no ID - skipping.");
                continue;
            }
            if (weaponLookup.ContainsKey(weapon.Id))
            {
                Debug.LogError($"Duplicate ID: {weapon.Id} on {weapon.name}");
                continue;
            }
            weaponLookup[weapon.Id] = weapon;
        }

        maskVisualLookup = new Dictionary<string, MaskVisualSO>();
        foreach (var mask in allMaskVisuals)
        {
            if (string.IsNullOrEmpty(mask.Id))
            {
                Debug.LogError($"Effector {mask.name} has no ID - skipping.");
                continue;
            }
            if (maskVisualLookup.ContainsKey(mask.Id))
            {
                Debug.LogError($"Duplicate ID: {mask.Id} on {mask.name}");
                continue;
            }
            maskVisualLookup[mask.Id] = mask;
        }
    }

    public StatusEffectorSO GetRandomStatusEffector()
    {
        int rand = Random.Range(0, statusLookup.Count);
        string randKey = statusLookup.Keys.ToList()[rand];
        return statusLookup[randKey];
    }

    public T Get<T>(string id)
    {
        if (TryGet<T>(id, out var item)) return item;
        Debug.LogError($"ItemDatabase: no item found for id '{id}'");
        return default(T);
    }

    public bool TryGet<T>(string id, out T item)
    {
        if (typeof(T) == typeof(StatusEffectorSO) || typeof(T).IsSubclassOf(typeof(StatusEffectorSO)))
        {
            if (statusLookup.TryGetValue(id, out var effector) && effector is T result)
            {
                item = result;
                return true;
            }
        }
        else if (typeof(T) == typeof(WeaponConfigSO) || typeof(T).IsSubclassOf(typeof(WeaponConfigSO)))
        {
            if (weaponLookup.TryGetValue(id, out var weapon) && weapon is T result)
            {
                item = result;
                return true;
            }
        }
        else if (typeof(T) == typeof(MaskVisualSO) || typeof(T).IsSubclassOf(typeof(MaskVisualSO)))
        {
            if (maskVisualLookup.TryGetValue(id, out var weapon) && weapon is T result)
            {
                item = result;
                return true;
            }
        }

        item = default;
        return false;
    }
}