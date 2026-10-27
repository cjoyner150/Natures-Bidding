using System;

public interface IInputStrategy
{
    public Action OnComplete { get; set; }
    public bool IsRunning { get; set; }

    public void Start();
    public void Tick(float deltaTime);
    public void Cancel();
}
