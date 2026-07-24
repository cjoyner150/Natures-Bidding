using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DroppingPlatforms : NetworkBehaviour, IHazardSystem
{
    [SerializeField] List<Transform> platforms;

    public void StopHazard()
    {
        throw new System.NotImplementedException();
    }

    public void TickHazard(float delta)
    {
        throw new System.NotImplementedException();
    }

    public void TriggerHazard()
    {
        throw new System.NotImplementedException();
    }


}
