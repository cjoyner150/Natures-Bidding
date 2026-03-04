using HSM;
using UnityEngine;

public class Fall : State
{
    private readonly PlayerContext ctx;
    // FallAttack

    int attacksWhileFalling;

    public Fall(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        attacksWhileFalling = 0;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.forceToAdd = (ctx.moveInput * ctx.acceleration * ctx.airControlMultiplier);
        HandleRotation(deltaTime);
    }

    void HandleRotation(float deltaTime)
    {
        ctx.anim.transform.forward = Vector3.Slerp(ctx.anim.transform.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }
}