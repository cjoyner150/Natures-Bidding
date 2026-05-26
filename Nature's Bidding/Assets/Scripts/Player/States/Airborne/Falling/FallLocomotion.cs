using HSM;
using UnityEngine;

public class FallLocomotion : State
{
    private readonly PlayerContext ctx;

    public FallLocomotion(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.desiredMaxSpeed = ctx.airSpeed * ctx.playerStats.MoveSpeed;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.forceToAdd = (ctx.moveInput * ctx.acceleration * ctx.airControlMultiplier) + (-ctx.modelHolder.transform.up * ctx.acceleration * ctx.extraGravityMultiplier);
        HandleRotation(deltaTime);
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    protected override State GetTransition()
    {
        State transition = (ctx.attackPressed && !ctx.attackOnCooldown) ? GetParentOfType<Fall>().fallAttack : null;

        transition ??= (ctx.dashPressed && !ctx.dashOnCooldown) ? GetParentOfType<Airborne>().airDash : null;

        return transition;

    }
}