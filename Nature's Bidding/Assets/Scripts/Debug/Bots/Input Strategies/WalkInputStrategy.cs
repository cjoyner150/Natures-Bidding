using UnityEngine;

public class WalkInputStrategy : BaseDummyInputStrategy
{
    float timer;

    public WalkInputStrategy(PlayerContext ctx) : base(ctx) { }

    protected override bool IsDone()
    {
        return timer <= 0;
    }
    
    protected override void OnStart()
    {
        timer = 3f;
    }

    protected override void OnTick(float deltaTime)
    {
        timer -= deltaTime;
        ctx.moveInput = new Vector3(0, 0, -1);
    }

    protected override void OnEnd()
    {
        ctx.moveInput = Vector3.zero;
        ctx.rb.linearVelocity = Vector3.zero;
    }
}
