using UnityEngine;

public interface IHazardSystem
{
    virtual void StartHazard() { }
    virtual void StopHazard() { }
    void TickHazard(float delta);
    void TriggerHazard();
}
