using UnityEngine;
using HSM;
public class JumpAttack : State
{
    private readonly PlayerContext ctx;

    private Vector3 momentumDirection;
    private Vector3 facingDirection;
    private float attackTimer;
    private bool exitAttack;

    public JumpAttack(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.desiredMaxSpeed = ctx.attackSpeed;
        ctx.forceMode = ForceMode.Force;

        ctx.rb.useGravity = false;

        ctx.anim.SetTrigger("JumpAttack");

        facingDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;
        momentumDirection = (facingDirection + ctx.modelHolder.up).normalized;

        attackTimer = ctx.jumpAttackTime;
        exitAttack = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        HandleRotation(deltaTime);

        ctx.forceToAdd = momentumDirection * ctx.acceleration;

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

        ctx.rb.useGravity = true;
    }

    protected override State GetTransition() 
    {
        if (exitAttack) return GetParentOfType<Airborne>().fall;
        else return null;
    }
}