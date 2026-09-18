using UnityEngine;

public interface IEffectable
{
    public void StealFrom(ulong thiefId, ulong targetId, int amount);
}
