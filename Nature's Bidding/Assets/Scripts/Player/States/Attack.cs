using UnityEngine;
using HSM;
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

        momentumDirection = ctx.moveInput.magnitude > 0.01f ? ctx.moveInput : ctx.anim.transform.forward;

        attackTimer = ctx.attackTime;
        exitAttack = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        ctx.forceToAdd = ctx.anim.transform.forward * ctx.acceleration * 10f;

        HandleRotation(deltaTime);

        attackTimer -= deltaTime;
        if (attackTimer <= 0) exitAttack = true;
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    void HandleRotation(float deltaTime)
    {
        ctx.anim.transform.forward = Vector3.Slerp(ctx.anim.transform.forward, momentumDirection, ctx.turnSpeed * deltaTime);
    }

    protected override State GetTransition() 
    {
        if (exitAttack) return GetParentOfType<Grounded>().groundedLocomotion;
        else return null;
    }

}