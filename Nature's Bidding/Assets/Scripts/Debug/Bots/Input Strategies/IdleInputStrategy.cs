public class IdleInputStrategy : BaseDummyInputStrategy
{
    public IdleInputStrategy(PlayerContext ctx) : base(ctx) { }

    protected override bool IsDone()
    {
        return true;
    }
}
