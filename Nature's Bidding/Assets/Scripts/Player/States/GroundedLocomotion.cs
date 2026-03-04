using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSM;

public class GroundedLocomotion : State
{
    private readonly PlayerContext ctx;
    public readonly Idle idle;
    public readonly Move move;

    public GroundedLocomotion(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        idle = new Idle(machine, ctx, this);
        move = new Move(machine, ctx, this);
    }

    protected override State GetInitialState() => ctx.moveInput.magnitude > 0.01f ? move : idle;

    protected override State GetTransition()
    {
        if (ctx.jumpPressed) return GetParentOfType<PlayerRoot>().airborne.jump;
        else return null;
    }

}

