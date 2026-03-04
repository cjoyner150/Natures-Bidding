using UnityEngine;
using HSM;
public class Move : State
{
    private readonly PlayerContext ctx;

    public Move(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.forceMode = ForceMode.Force;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.forceToAdd = ctx.moveInput * ctx.acceleration * 10f;

        HandleSpeedControl();
        HandleRotation(deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    void HandleSpeedControl()
    {
        float maxSpeed = ctx.sprintPressed ? ctx.sprintSpeed : ctx.moveSpeed;

        if (ctx.rb.linearVelocity.magnitude > maxSpeed)
        {
            ctx.rb.linearVelocity = ctx.rb.linearVelocity.normalized * maxSpeed;
        }
    }

    void HandleRotation(float deltaTime)
    {
        ctx.anim.transform.forward = Vector3.Slerp(ctx.anim.transform.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override State GetTransition() => ctx.moveInput.magnitude > 0.01f ? null : GetParentOfType<GroundedLocomotion>().idle;

}