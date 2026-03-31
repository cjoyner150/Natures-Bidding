using UnityEngine;

public interface IDamageable
{
    public enum HitCallbackContext
    {
        success,
        parried,
        failed
    }

    public void Hit(float damage, ulong fromPlayerId, out HitCallbackContext context);
}
