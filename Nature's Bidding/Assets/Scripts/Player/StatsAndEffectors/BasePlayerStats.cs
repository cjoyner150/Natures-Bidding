using UnityEngine;

[CreateAssetMenu(fileName = "New Base Player Stats", menuName="Stats/Base Stats")]
public class BasePlayerStats : ScriptableObject
{
    public float MaxHealth;
    public float Damage;
    public float AttackSpeed;
    public float MoveSpeed;
    public float ParryDuration;
    public float ParryCooldown;
    public float DashDistance;
    public float DashCooldown;
    public float CritChance;
    public float CritDamageMultiplier;
    public float Momentum;
    public float ComboDamage;
    public float Stealing;
    public float Lifesteal;
}
