using UnityEngine;
using HSM;
public class PlayerRoot : State
{
    private readonly PlayerContext ctx;
    //private readonly Grounded grounded;
    //private readonly Airborne airborne;

    public PlayerRoot(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx=ctx;


    }

    //protected override State GetInitialState() => grounded;
}
