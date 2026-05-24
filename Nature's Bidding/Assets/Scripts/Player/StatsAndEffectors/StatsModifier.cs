using System;

public abstract class StatsModifier : IDisposable
{
    public bool IsMarkedForRemoval { get; private set; }
    public event Action<StatsModifier> OnDispose = delegate { };

    readonly CountdownTimer timer;

    protected StatsModifier(float duration)
    {
        if (duration <= 0)
        {
            return;
        }

        timer = new CountdownTimer(duration);
        timer.OnTimerStop += Dispose;
        timer.Start();
    }

    public void Update(float deltaTime) => timer?.Tick(deltaTime);


    public abstract void Handle(object sender, Query query);

    public void Dispose()
    {
        IsMarkedForRemoval = true;
        OnDispose?.Invoke(this);
    }

}