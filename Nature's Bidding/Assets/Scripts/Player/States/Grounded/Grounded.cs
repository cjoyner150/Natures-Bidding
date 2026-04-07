using HSM;
using UnityEngine;

public class Grounded : State
{
    private readonly PlayerContext ctx;
    public readonly GroundedLocomotion groundedLocomotion;
    public readonly GroundedAction groundedAction;

    public Grounded(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        groundedLocomotion = new GroundedLocomotion(machine, ctx, this);
        groundedAction = new GroundedAction(machine, ctx, this);
    }

    protected override void OnEnter()
    {
        ctx.rb.linearDamping = ctx.groundDrag;
    }

    protected override State GetTransition()
    {
        if (ctx.shouldTakeKnockback) return groundedAction.knockback;
        else if (!ctx.isGrounded) return GetParentOfType<PlayerRoot>().airborne;
        else return null;
    }

    protected override State GetInitialState() => groundedLocomotion;
}