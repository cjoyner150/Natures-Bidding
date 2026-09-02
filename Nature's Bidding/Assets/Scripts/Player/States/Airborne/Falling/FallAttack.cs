using Cysharp.Threading.Tasks;
using HSM;
using UnityEngine;

public class FallAttack : State
{
    private readonly PlayerContext ctx;

    private Vector3 momentumDirection;
    private Vector3 facingDirection;
    private float attackTimer;

    public FallAttack(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.attackPressed = false;
        ctx.desiredMaxSpeed = ctx.attackSpeed * ctx.playerStats.MoveSpeed;
        ctx.forceMode = ForceMode.Force;

        ctx.anim.SetTrigger("FallAttack");
        ctx.anim.SetFloat("AttackSpeed", 1 + ((ctx.playerStats.AttackSpeed - 1) / 2f));
        ctx.playerAttackManager.BeginAttack();

        facingDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;
        momentumDirection = facingDirection.normalized;

        attackTimer = ctx.fallAttackTime / ctx.playerStats.AttackSpeed;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.forceToAdd = (ctx.moveInput * ctx.acceleration * ctx.airControlMultiplier * .5f) + (-ctx.modelHolder.transform.up * ctx.acceleration * ctx.extraGravityMultiplier);
        HandleRotation(deltaTime);

        attackTimer -= deltaTime;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, facingDirection.normalized, ctx.turnSpeed * deltaTime);
    }

    protected override void OnExit()
    {
        GameLogger.Log(LogSeverity.Verbose, $"exiting after {attackTimer}");

        ctx.rb.useGravity = true;
        ctx.forceToAdd = Vector3.zero;
        ctx.attackCDTimer = ctx.attackCD / ctx.playerStats.AttackSpeed;

        try { ctx.playerAttackManager.EndAttack(); }
        catch (System.Exception e) { GameLogger.LogException(LogSeverity.Error, "An unexpected error occurred while ending an attack.", e); }

        if (ctx.isGrounded)
        {
            ctx.anim.SetTrigger("FallSlam");

            try { ctx.playerAttackManager.FallingSlamAttack(); }
            catch (System.Exception e) { GameLogger.LogException(LogSeverity.Error, "An unexpected error occurred while performing a falling slam attack.", e); }
        }
        else
        {
            ctx.anim.SetTrigger("FallAttackCancel");
        }
    }

    protected override State GetTransition()
    {
        return ctx.isGrounded ? GetParentOfType<PlayerRoot>().grounded.groundedAction.slamRecovery : null;
    }
}