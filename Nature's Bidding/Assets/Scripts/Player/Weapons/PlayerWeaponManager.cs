using UnityEngine;
using Unity.Netcode;
using System.Linq;
using UnityUtils;
using Cysharp.Threading.Tasks;

public class PlayerWeaponManager : MonoBehaviour
{
    [SerializeField] Transform weaponHolderTransform;

    PlayerStatusEffectManager playerStatusEffectManager;
    Weapon currentWeapon = null;

    public void Initialize(PlayerStatusEffectManager sm)
    {
        playerStatusEffectManager = sm;
    }

    public async UniTask EquipWeapon(string weaponId)
    {
        GameLogger.Log(LogSeverity.Debug, $"EquipWeapon START. LocalClientId={NetworkManager.Singleton.LocalClientId}, weaponId={weaponId}");

        var go = await NetworkedWeaponFactory.Instance.EquipWeapon(NetworkManager.Singleton.LocalClientId, weaponId);

        GameLogger.Log(LogSeverity.Debug, $"EquipWeapon got result. go={(go == null ? "NULL" : go.name)}");

        if (go == null)
        {
            GameLogger.Log(LogSeverity.Warning, $"Failed to equip weapon '{weaponId}'.");
            return;
        }

        UnequipWeapon();

        var so = GameDataManager.Instance.GetWeapon(weaponId);
        currentWeapon = new Weapon(so, go);

        if (!currentWeapon.HeldEffects.IsNullOrEmpty())
        {
            playerStatusEffectManager.AddModifiers(currentWeapon.HeldEffects);
        }
    }

    public void UnequipWeapon()
    {
        if (currentWeapon == null) return;

        if (!currentWeapon.HeldEffects.IsNullOrEmpty())
        {
            playerStatusEffectManager.RemoveModifiers(currentWeapon.HeldEffects.Select(e => e.Id));
        }

        NetworkedWeaponFactory.Instance.UnequipWeapon(NetworkManager.Singleton.LocalClientId);
        currentWeapon = null;
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
