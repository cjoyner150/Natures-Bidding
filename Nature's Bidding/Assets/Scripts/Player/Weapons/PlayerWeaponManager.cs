using UnityEngine;
using Unity.Netcode;
using System.Linq;
using UnityUtils;

public class PlayerWeaponManager : MonoBehaviour
{
    [SerializeField] Transform weaponHolderTransform;

    PlayerStatusEffectManager playerStatusEffectManager;
    WeaponFactory weaponFactory = new();
    Weapon currentWeapon = null;

    public void Initialize(PlayerStatusEffectManager sm)
    {
        playerStatusEffectManager = sm;
    }

    public void EquipWeapon(WeaponConfigSO so)
    {
        if (weaponFactory.CreateWeapon(so, weaponHolderTransform, out var weapon))
        {
            UnequipWeapon();
            currentWeapon = weapon;

            if (!currentWeapon.HeldEffects.IsNullOrEmpty())
            {
                playerStatusEffectManager.AddModifiers(currentWeapon.HeldEffects);
            }
        }
    }

    public void EquipWeapon(string weaponId)
    {
        var so = GameDataManager.Instance.GetWeapon(weaponId);
        EquipWeapon(so);
    }

    public void UnequipWeapon()
    {
        if (currentWeapon != null)
        {
            if (!currentWeapon.HeldEffects.IsNullOrEmpty())
            {
                playerStatusEffectManager.RemoveModifiers(currentWeapon.HeldEffects.Select(e => e.Id));
            }

            Destroy(currentWeapon.WeaponGameObject);
            currentWeapon = null;
        }
    }

    public bool TryRemoveWeapon(string weaponId)
    {
        if (currentWeapon == null) return false;

        if (currentWeapon.Id == weaponId)
        {
            UnequipWeapon();
            return true;
        }

        return false;
    }
}
