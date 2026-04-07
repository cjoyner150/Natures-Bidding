using UnityEngine;
using HSM;
public class Dash : State
{
    private readonly PlayerContext ctx;
    
    private Vector3 momentumDirection;
    private bool exitDash;
    private float dashTimer;

    public Dash(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.anim.SetBool("Dashing", true);

        ctx.desiredMaxSpeed = ctx.dashSpeed;

        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;

        dashTimer = ctx.dashTime;
        exitDash = false;
    }

    protected override void OnExit()
    {
        ctx.anim.SetBool("Dashing", false);

        ctx.dashCDTimer = ctx.dashCD;
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
        if (exitDash) return GetParentOfType<Grounded>().groundedLocomotion;
        else return null;
    }

}