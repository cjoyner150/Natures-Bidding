using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSM;

public class GroundedLocomotion : State
{
    private readonly PlayerContext ctx;
    private readonly Idle idle;
    private readonly Move move;

    public GroundedLocomotion(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }
}

