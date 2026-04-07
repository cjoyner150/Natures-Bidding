using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSM;

public class GroundedAction : State
{
    private readonly PlayerContext ctx;
    public readonly Attack attack;
    public readonly Dash dash;
    public readonly Knockback knockback;

    public GroundedAction(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;

        attack = new Attack(machine, ctx, this);
        dash = new Dash(machine, ctx, this);
        knockback = new Knockback(machine, ctx, this);
    }

    protected override State GetInitialState() 
    {
        if (ctx.attackPressed) return attack;
        else if (ctx.dashPressed) return dash;
        else return null;
    }
}

