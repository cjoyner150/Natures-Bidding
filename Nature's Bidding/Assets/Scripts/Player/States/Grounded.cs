using HSM;
public class Grounded : State
{
    private readonly PlayerContext ctx;
    public readonly GroundedLocomotion groundedLocomotion;

    public Grounded(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        groundedLocomotion = new GroundedLocomotion(machine, ctx, this);
    }

    protected override void OnEnter()
    {
        ctx.rb.linearDamping = ctx.groundDrag;
    }

    protected override State GetTransition()
    {
        if (!ctx.isGrounded) return GetParentOfType<PlayerRoot>().airborne;
        else return null;
    }

    protected override State GetInitialState() => groundedLocomotion;
}