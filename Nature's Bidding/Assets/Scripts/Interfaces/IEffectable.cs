using UnityEngine;

public interface IEffectable
{
    /// <summary>
    /// Steals gold from the effectable target
    /// </summary>
    public void StealFrom(long thiefId, long targetId, int amount);

    /// <summary>
    /// Applies the stun condition to a damageable
    /// </summary>
    public void Stun(float additionalStunTime);

    /// <summary>
    /// Recovers from the stun condition
    /// </summary>
    public void Recover();
}
