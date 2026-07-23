using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Basic Weapon", menuName = "Weapons/Basic Weapon")]
public class WeaponConfigSO : ScriptableObject
{
    public string Id;
    public string Name;
    public List<StatusEffectorSO> heldEffects = new();
    public List<StatusEffectorSO> applyEffects = new();
    public GameObject weaponPrefab;
    public GameObject weaponCollectablePrefab;
    public Sprite weaponSprite;
}
