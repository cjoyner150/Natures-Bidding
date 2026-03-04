using HSM;
public class Airborne : State
{
    private readonly PlayerContext ctx;
    public readonly Jump jump;
    public readonly Fall fall;

    private float regroundedCooldown = .2f;
    private bool canGround;

    public Airborne(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        jump = new Jump(machine, ctx, this);
        fall = new Fall(machine, ctx, this);
    }

    protected override void OnEnter()
    {
        ctx.rb.linearDamping = ctx.airDrag;
        regroundedCooldown = .2f;
        canGround = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (!canGround)
        {
            regroundedCooldown -= deltaTime;

            if (regroundedCooldown <= 0)
            {
                canGround = true;
            }
        }
    }

    protected override State GetInitialState() => ctx.jumpPressed ? jump : fall;

    protected override State GetTransition() => ctx.isGrounded && canGround ? GetParentOfType<PlayerRoot>().grounded : null;
}