using HSM;
public class Airborne : State
{
    private readonly PlayerContext ctx;
    public readonly Jump jump;
    public readonly Fall fall;
    public readonly AirDash airDash;
    public readonly AirKnockback airKnockback;

    private float regroundedCooldown = .2f;
    private bool canGround;

    public Airborne(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;

        jump = new Jump(machine, ctx, this);
        fall = new Fall(machine, ctx, this);
        airDash = new AirDash(machine, ctx, this);
        airKnockback = new AirKnockback(machine, ctx, this);
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

    protected override State GetInitialState()
    {
        if (ctx.shouldTakeKnockback) return airKnockback;
        else if (ctx.jumpPressed) return jump;
        else return fall;
    }

    protected override State GetTransition() 
    {
        if (ctx.shouldTakeKnockback) return airKnockback;
        return (ctx.isGrounded && canGround) ? GetParentOfType<PlayerRoot>().grounded : null; 
    }

}