using UnityEngine;
using HSM;
public class PlayerRoot : State
{
    private readonly PlayerContext ctx;
    public readonly Grounded grounded;
    public readonly Airborne airborne;

    public PlayerRoot(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx=ctx;

        grounded = new Grounded(machine, ctx, this);
        airborne = new Airborne(machine, ctx, this);
    }

    protected override void OnUpdate(float deltaTime)
    {
        HandleSpeedControl();
        HandleMomentumConservation(deltaTime);
        HandleActionCooldowns(deltaTime);
    }

    void HandleSpeedControl()
    {
        Vector3 horizontalVelocity = ctx.rb.linearVelocity;
        horizontalVelocity.y = 0;

        if (horizontalVelocity.magnitude > ctx.currentMaxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * ctx.currentMaxSpeed;
            ctx.rb.linearVelocity = new Vector3(horizontalVelocity.x, ctx.rb.linearVelocity.y, horizontalVelocity.z);
        }
    }

    void HandleMomentumConservation(float deltaTime)
    {
        if (ctx.currentMaxSpeed > ctx.desiredMaxSpeed)
        {
            ctx.currentMaxSpeed = Mathf.LerpUnclamped(ctx.currentMaxSpeed, ctx.desiredMaxSpeed, deltaTime * ctx.momentumLerpSpeed);
        }
        else ctx.currentMaxSpeed = ctx.desiredMaxSpeed;
    }

    void HandleActionCooldowns(float deltaTime)
    {
        if (ctx.attackOnCooldown)
        {
            ctx.attackCDTimer -= deltaTime;
        }
        if (ctx.dashOnCooldown)
        {
            ctx.dashCDTimer -= deltaTime;
        }
        if (ctx.parryOnCooldown)
        {
            ctx.parryCDTimer -= deltaTime;
        }
    }

    protected override State GetInitialState() => ctx.isGrounded ? grounded : airborne;
}
