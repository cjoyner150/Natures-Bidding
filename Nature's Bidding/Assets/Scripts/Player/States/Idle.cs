using HSM;
public class Idle : State
{
    private readonly PlayerContext ctx;

    public Idle(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override State GetTransition() => ctx.moveInput.magnitude > 0.01f ? GetParentOfType<GroundedLocomotion>().move : null;

}