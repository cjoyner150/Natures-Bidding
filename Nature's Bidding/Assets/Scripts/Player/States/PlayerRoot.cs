using UnityEngine;
using HSM;
public class PlayerRoot : State
{
    private readonly PlayerContext ctx;
    public readonly Grounded grounded;
    public readonly Airborne airborne;

    public PlayerRoot(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx=ctx;

        grounded = new Grounded(machine, ctx, this);
        airborne = new Airborne(machine, ctx, this);
    }

    protected override State GetInitialState() => ctx.isGrounded ? grounded : airborne;
}
