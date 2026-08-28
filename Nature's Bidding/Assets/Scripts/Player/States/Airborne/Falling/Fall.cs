using HSM;
using UnityEngine;

public class Fall : State
{
    private readonly PlayerContext ctx;
    public readonly FallLocomotion fallLocomotion;
    public readonly FallAttack fallAttack;

    public Fall(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        fallLocomotion = new FallLocomotion(machine, ctx, this);
        fallAttack = new FallAttack(machine, ctx, this);
    }

    protected override void OnEnter()
    {
        ctx.desiredMaxSpeed = ctx.airSpeed * ctx.playerStats.MoveSpeed;
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
    }

    protected override State GetInitialState() {
        bool result = ctx.attackPressed && !ctx.attackOnCooldown;
        GameLogger.Log(LogSeverity.Verbose, $"[GetInitialState] attackPressed={ctx.attackPressed}, attackOnCooldown={ctx.attackOnCooldown}, attackCDTimer={ctx.attackCDTimer} → {(result ? "fallAttack" : "fallLocomotion")}");
        return result ? fallAttack : fallLocomotion;
    }

    protected override State GetTransition()
    {
        if (ctx.shouldStunSelf || ctx.isStunned) return GetParentOfType<Airborne>().airborneStunned;
        else return null;
    }
}