using UnityEngine;

public interface IDamageable
{
    public enum HitCallbackContext
    {
        success,
        parried,
        failed
    }

    public void Hit(float damage, PlayerContext fromPlayerCtx, out HitCallbackContext context, bool critical = false);

    public void TickHealth(float damage, PlayerContext fromPlayerCtx);

    public void Heal(float amount);

    public void BeginParry();
    public void EndParry();
}
