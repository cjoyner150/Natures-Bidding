using UnityEngine;

public interface IDamageable
{
    public enum HitCallbackContext
    {
        success,
        parried,
        failed
    }

    public void Hit(float damage, ulong fromPlayerId, out HitCallbackContext context, bool critical = false);

    public void TickHealth(float damage, ulong fromPlayerId);
}
