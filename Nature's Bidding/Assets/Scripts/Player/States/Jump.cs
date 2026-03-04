using UnityEngine;
using HSM;
public class Jump : State
{
    private readonly PlayerContext ctx;

    private bool spaceHeld;
    private float spaceHeldTimer;

    public Jump(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.rb.AddForce(ctx.jumpImpulse * ctx.rb.transform.up, ForceMode.Impulse);
        spaceHeld = true;
        spaceHeldTimer = ctx.jumpHeldAllowedTime;
    }

    protected override void OnUpdate(float deltaTime)
    {
        HandleRotation(deltaTime);

        if (!spaceHeld) { ctx.forceToAdd = Vector3.zero; return; }

        spaceHeld = ctx.jumpPressed;
        spaceHeldTimer -= deltaTime;

        if (spaceHeldTimer <= 0) spaceHeld = false;

        ctx.forceToAdd = (ctx.moveInput * ctx.acceleration * ctx.airControlMultiplier) + (ctx.jumpHeldForce * ctx.rb.transform.up);
    }

    void HandleRotation(float deltaTime)
    {
        ctx.anim.transform.forward = Vector3.Slerp(ctx.anim.transform.forward, ctx.moveInput.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    protected override State GetTransition() => ctx.rb.linearVelocity.y < 0 ? GetParentOfType<Airborne>().fall : null;
}