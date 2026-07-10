using UnityEngine;

public class WeaponFactory
{
    public bool CreateWeapon(WeaponConfigSO so, Transform parent, out Weapon weapon)
    {
        weapon = null;

        if (so == null || so.weaponPrefab == null) return false;

        var go = Object.Instantiate(so.weaponPrefab, parent);
        if (go == null) return false;

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        weapon = new Weapon(so, go);
        return true;
    }

    public bool CreateWeaponCollectable(WeaponConfigSO so, Vector3 position, Quaternion rotation)
    {
        if (so != null && so.weaponCollectablePrefab != null)
        {
            var go = Object.Instantiate(so.weaponCollectablePrefab, position, rotation);
            if (go != null) return true;
        }

        return false;
    }
}

