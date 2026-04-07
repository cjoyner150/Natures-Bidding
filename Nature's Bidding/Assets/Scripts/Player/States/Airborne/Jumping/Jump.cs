using UnityEngine;
using HSM;
public class Jump : State
{
    private readonly PlayerContext ctx;
    public readonly JumpAttack jumpAttack;
    public readonly JumpLocomotion jumpLocomotion;

    public Jump(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        jumpAttack = new JumpAttack(machine, ctx, this);
        jumpLocomotion = new JumpLocomotion(machine, ctx, this);
    }

    protected override void OnEnter()
    {
        ctx.anim.SetTrigger("Jump");
        ctx.rb.AddForce(ctx.jumpImpulse * ctx.rb.transform.up, ForceMode.Impulse);
    }

    protected override State GetInitialState() => (ctx.attackPressed && !ctx.attackOnCooldown) ? jumpAttack : jumpLocomotion;
}