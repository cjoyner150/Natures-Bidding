using UnityEngine;

public interface IHazardSystem
{
    virtual void StartHazard() { }
    void StopHazard();
    void TickHazard(float delta);
    void TriggerHazard();
}
