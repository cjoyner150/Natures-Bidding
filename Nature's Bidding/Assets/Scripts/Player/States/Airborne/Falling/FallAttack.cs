using HSM;
using UnityEngine;

public class FallAttack : State
{
    private readonly PlayerContext ctx;

    private Vector3 momentumDirection;
    private Vector3 facingDirection;
    private float attackTimer;
    private bool exitAttack;

    public FallAttack(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        Add(new PauseInAirActivity(ctx, .2f));
    }

    protected override void OnEnter()
    {
        ctx.attackPressed = false;
        ctx.desiredMaxSpeed = ctx.attackSpeed;

        ctx.anim.SetTrigger("FallAttack");
        ctx.playerAttackManager.BeginAttack();

        facingDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;
        momentumDirection = (facingDirection + -ctx.modelHolder.up).normalized;

        attackTimer = ctx.fallAttackTime;
        exitAttack = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.rb.linearVelocity = momentumDirection * ctx.desiredMaxSpeed;
        HandleRotation(deltaTime);

        attackTimer -= deltaTime;
        if (attackTimer <= 0) exitAttack = true;

    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, facingDirection.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
        ctx.attackCDTimer = ctx.attackCD;

        ctx.playerAttackManager.EndAttack();
        ctx.rb.useGravity = true;
    }

    protected override State GetTransition()
    {
        if (exitAttack) return GetParentOfType<Airborne>().fall;
        else return null;
    }
}