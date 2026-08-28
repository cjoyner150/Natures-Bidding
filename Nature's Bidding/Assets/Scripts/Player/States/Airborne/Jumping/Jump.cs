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
        ctx.currentJumps--;
        GameLogger.Log(LogSeverity.Debug, $"Jumping! Jumps now {ctx.currentJumps}");
        ctx.anim.SetTrigger("Jump");
        NetworkVisualEffectManager.SpawnJumpEffectsOnPlayer?.Invoke(ctx.playerHealth.OwnerClientId);

        Vector3 vel = ctx.rb.linearVelocity;
        vel.y = Mathf.Sqrt(2f * Physics.gravity.magnitude * ctx.jumpHeight);
        ctx.rb.linearVelocity = vel;
    }

    protected override State GetInitialState() => (ctx.attackPressed && !ctx.attackOnCooldown) ? jumpAttack : jumpLocomotion;

    protected override State GetTransition()
    {
        if (ctx.shouldStunSelf || ctx.isStunned) return GetParentOfType<Airborne>().airborneStunned;
        else return null;
    }
}