using UnityEngine;
using HSM;
public class AirKnockback : State
{
    private readonly PlayerContext ctx;

    private Vector3 momentumDirection;
    private Vector3 facingDirection;
    private bool exitKnockback;
    private float knockbackTimer;

    public AirKnockback(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.shouldTakeKnockback = false;
        ctx.desiredMaxSpeed = ctx.knockbackSpeed;

        momentumDirection = ctx.rb.transform.position - ctx.lastHitFromPosition;
        momentumDirection.y = 0;
        momentumDirection = momentumDirection.normalized;

        facingDirection = -momentumDirection;

        knockbackTimer = ctx.knockbackTime;
        exitKnockback = false;
    }

    protected override void OnExit()
    {

    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.rb.linearVelocity = momentumDirection * ctx.desiredMaxSpeed;

        HandleRotation(deltaTime);

        knockbackTimer -= deltaTime;
        if (knockbackTimer <= 0) exitKnockback = true;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, facingDirection, ctx.turnSpeed * deltaTime * ctx.dashRotateMultiplier);
    }

    protected override State GetTransition()
    {
        if (exitKnockback) return GetParentOfType<Airborne>().fall;
        else return null;
    }

}