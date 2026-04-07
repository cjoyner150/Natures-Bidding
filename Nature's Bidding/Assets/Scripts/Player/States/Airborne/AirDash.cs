using UnityEngine;
using HSM;
public class AirDash : State
{
    private readonly PlayerContext ctx;
    
    private Vector3 momentumDirection;
    private bool exitDash;
    private float dashTimer;

    public AirDash(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.anim.SetBool("AirDashing", true);

        ctx.desiredMaxSpeed = ctx.dashSpeed;

        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;

        dashTimer = ctx.dashTime;
        exitDash = false;

        ctx.rb.useGravity = false;
    }

    protected override void OnExit()
    {
        ctx.anim.SetBool("AirDashing", false);

        ctx.dashCDTimer = ctx.dashCD;
        ctx.rb.useGravity = true;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.rb.linearVelocity = momentumDirection * ctx.desiredMaxSpeed;

        HandleRotation(deltaTime);

        dashTimer -= deltaTime;
        if (dashTimer <= 0) exitDash = true;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, momentumDirection, ctx.turnSpeed * deltaTime * ctx.dashRotateMultiplier);
    }

    protected override State GetTransition() 
    {
        if (exitDash) return GetParentOfType<Airborne>().fall;
        else return null;
    }

}