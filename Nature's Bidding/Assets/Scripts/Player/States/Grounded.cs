using HSM;
public class Grounded : State
{
    private readonly PlayerContext ctx;
    private readonly GroundedLocomotion groundedLocomotion;

    public Grounded(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        groundedLocomotion = new GroundedLocomotion(machine, ctx, this);
    }

    protected override State GetInitialState() => groundedLocomotion;
}