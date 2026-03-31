using UnityEngine;

public class NetworkTimer
{
    private float timer;
    
    public float MinimumTimeBetweenTicks { get; }
    public int Tick { get; set; }
    
    public NetworkTimer(float tickRate)
    {
        MinimumTimeBetweenTicks = 1f / tickRate;
    }

    public void Update(float deltaTime)
    {
        timer += deltaTime;
    }
    
    public bool ShouldTick()
    {
        if (timer >= MinimumTimeBetweenTicks)
        {
            timer -= MinimumTimeBetweenTicks;
            Tick++;
            
            return true;
        }
        
        return false;
    }
}
