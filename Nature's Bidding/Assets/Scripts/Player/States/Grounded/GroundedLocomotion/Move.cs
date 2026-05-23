using UnityEngine;
using HSM;
public class Move : State
{
    private readonly PlayerContext ctx;

    float idleTimer = 0;
    bool isIdle = false;

    public Move(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.forceMode = ForceMode.Force;
        ctx.anim.SetBool("Walking", true);
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (ctx.moveInput.magnitude < 0.01f && !isIdle)
        {
            idleTimer += deltaTime;

            if (idleTimer > .5f)
            {
                isIdle = true;
                idleTimer = 0;
            }
        }
        else
        {
            idleTimer = 0;
        }

        ctx.desiredMaxSpeed = ctx.moveInputIsSprint ? ctx.sprintSpeed * ctx.playerStats.MoveSpeed : ctx.walkSpeed * ctx.playerStats.MoveSpeed;

        ctx.forceToAdd = ctx.moveInput * ctx.acceleration * 10f;

        HandleRotation(deltaTime);
    }

    protected override void OnExit()
    {
        ctx.anim.SetBool("Walking", false);
        ctx.forceToAdd = Vector3.zero;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override State GetTransition() => ctx.moveInput.magnitude > 0.01f ? null : GetParentOfType<GroundedLocomotion>().idle;

}