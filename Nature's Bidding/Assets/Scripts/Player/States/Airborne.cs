using HSM;
public class Airborne : State
{
    private readonly PlayerContext ctx;

    public Airborne(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
    }
}