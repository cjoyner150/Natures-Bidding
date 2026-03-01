using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Collectable", menuName = "Collectables/Weapon")]
public class WeaponSO : CollectableSO
{
    public int BaseDamage;
    public int HeavyDamage;
    public int ComboDamage;
}
