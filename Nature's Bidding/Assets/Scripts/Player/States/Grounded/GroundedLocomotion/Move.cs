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
        ctx.desiredMaxSpeed = ctx.moveInputIsSprint ? ctx.sprintSpeed : ctx.walkSpeed;

        ctx.forceToAdd = ctx.moveInput * ctx.acceleration * 10f;

        HandleRotation(deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override State GetTransition() => ctx.moveInput.magnitude > 0.01f ? null : GetParentOfType<GroundedLocomotion>().idle;

}