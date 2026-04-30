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
        ctx.desiredMaxSpeed = ctx.attackSpeed;
        ctx.forceMode = ForceMode.Force;

        ctx.anim.SetTrigger("Attack");
        SetAttackActive(ctx.attackActiveDelay);

        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.modelHolder.forward;

        attackTimer = ctx.attackTime;
        exitAttack = false;
    }

    async void SetAttackActive(int delay)
    {
        await UniTask.Delay(delay);
        ctx.playerAttackManager.BeginAttack();
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (ctx.hitResponse)
        {
            exitAttack = true;
            return;
        }

        ctx.forceToAdd = ctx.modelHolder.forward * ctx.acceleration * 10f;

        HandleRotation(deltaTime);

        attackTimer -= deltaTime;
        if (attackTimer <= 0) exitAttack = true;
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
        ctx.attackCDTimer = ctx.attackCD;
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