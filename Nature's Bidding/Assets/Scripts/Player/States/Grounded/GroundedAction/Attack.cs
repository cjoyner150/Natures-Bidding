using UnityEngine;
using HSM;
using Cysharp.Threading.Tasks;

public class Attack : State
{
    private readonly PlayerContext ctx;
    
    private Vector3 momentumDirection;
    private bool exitAttack;
    private float attackTimer;

    public Attack(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.desiredMaxSpeed = ctx.attackSpeed * ctx.playerStats.MoveSpeed;
        ctx.forceMode = ForceMode.Force;

        ctx.anim.SetTrigger("Attack");
        ctx.anim.SetFloat("AttackSpeed", 1 + ((ctx.playerStats.AttackSpeed - 1) / 2f));
        ctx.playerAttackManager.BeginAttack();

        attackTimer = ctx.attackTime / ctx.playerStats.AttackSpeed;
        exitAttack = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;

        if (ctx.hitResponse)
        {
            exitAttack = true;
            return;
        }

        ctx.forceToAdd = (momentumDirection * .5f + ctx.modelHolder.forward * .5f) * ctx.acceleration * 10f;

        HandleRotation(deltaTime);

        attackTimer -= deltaTime;
        if (attackTimer <= 0) exitAttack = true;
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
        ctx.attackCDTimer = ctx.attackCD / ctx.playerStats.AttackSpeed;
        ctx.playerAttackManager.EndAttack();

        ctx.hitResponse = false;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.modelHolder.forward = Vector3.Slerp(ctx.modelHolder.forward, momentumDirection, ctx.turnSpeed * deltaTime);
    }

    protected override State GetTransition() 
    {
        if (exitAttack) return GetParentOfType<Grounded>().groundedLocomotion;
        else return null;
    }

}