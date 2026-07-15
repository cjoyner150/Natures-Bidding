using UnityEngine;
using UnityEngine.InputSystem;

public class TheWeaponTarotStatusEffect : StatusEffect
{
    PlayerWeaponManager playerWeaponManager;
    public override StatsModifier GetStatsModifier() => null;

    public string weaponIdToEquip;

    public TheWeaponTarotStatusEffect(string weaponIdToEquip)
    {
        this.weaponIdToEquip = weaponIdToEquip;
    }

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null) return;

        playerWeaponManager = player.GetComponent<PlayerWeaponManager>();

        if (playerWeaponManager == null)
        {
            Debug.LogError($"[TheWeaponTarotStatusEffect] PlayerWeaponManager missing on {player.name} during OnInitialize.");
            return;
        }

        playerWeaponManager.EquipWeapon(weaponIdToEquip);
    }

    public override void OnEnd()
    {
        playerWeaponManager.TryRemoveWeapon(weaponIdToEquip);
    }
}
