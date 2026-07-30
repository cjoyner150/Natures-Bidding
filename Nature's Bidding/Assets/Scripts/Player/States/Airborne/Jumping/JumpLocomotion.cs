using UnityEngine;
using HSM;
public class JumpLocomotion : State
{
    private readonly PlayerContext ctx;

    private bool spaceHeld;
    private float spaceHeldTimer;
    private float checkFallDelay;

    public JumpLocomotion(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        checkFallDelay = .1f;

        ctx.desiredMaxSpeed = ctx.airSpeed * ctx.playerStats.MoveSpeed;
        ctx.forceMode = ForceMode.Force;
        spaceHeld = true;
        spaceHeldTimer = ctx.jumpHeldAllowedTime;
    }

    protected override void OnUpdate(float deltaTime)
    {
        checkFallDelay -= deltaTime;

        HandleRotation(deltaTime);

        if (!spaceHeld) { ctx.forceToAdd = (ctx.moveInput * (ctx.acceleration * ctx.airControlMultiplier)); return; }

        spaceHeld = ctx.jumpPressed;
        spaceHeldTimer -= deltaTime;

        if (spaceHeldTimer <= 0) spaceHeld = false;

        ctx.forceToAdd = (ctx.moveInput * (ctx.acceleration * ctx.airControlMultiplier)) + (ctx.jumpHeldForce * ctx.rb.transform.up);
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    protected override State GetTransition() { 
        State transition = (ctx.rb.linearVelocity.y < 0.001f && checkFallDelay < 0) ? GetParentOfType<Airborne>().fall : null;

        transition ??= (ctx.attackPressed && !ctx.attackOnCooldown) ? GetParentOfType<Jump>().jumpAttack : null;
        transition ??= (ctx.dashPressed && !ctx.dashOnCooldown) ? GetParentOfType<Airborne>().airDash : null;

        return transition;
    }
}