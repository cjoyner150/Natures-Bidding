using System;

public abstract class BaseDummyInputStrategy : IInputStrategy
{
    public Action OnComplete { get; set; }
    public bool IsRunning { get; set; }
    protected PlayerContext ctx;
    public BaseDummyInputStrategy(PlayerContext ctx)
    {
        this.ctx = ctx;
    }

    public void Start()
    {
        IsRunning = true;
        OnStart();
    }

    public void Tick(float deltaTime)
    {
        OnTick(deltaTime);

        if (IsDone())
        {
            End();
        }
    }

    protected void End()
    {
        IsRunning = false;
        OnEnd();

        OnComplete?.Invoke();
        OnComplete = () => { };
    }

    public void Cancel()
    {
        IsRunning = false;
        OnCancel();

        OnComplete = () => { };
    }

    protected virtual void OnStart() { }
    protected virtual void OnTick(float deltaTime) { }
    protected virtual void OnEnd() { }
    protected virtual void OnCancel() { }
    protected abstract bool IsDone();
}
