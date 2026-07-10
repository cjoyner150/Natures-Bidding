using System.Collections.Generic;
using UnityEngine;

public class Weapon
{
    public string Id { get; }
    public string Name { get; }
    public GameObject WeaponGameObject { get; }

    public List<StatusEffectorSO> HeldEffects = new();
    public List<StatusEffectorSO> ApplyEffects = new();

    public Weapon(WeaponConfigSO so, GameObject go)
    {
        Id = so.Id;
        Name = so.Name;
        HeldEffects = so.heldEffects;
        ApplyEffects = so.applyEffects;
        WeaponGameObject = go;
    }
}
