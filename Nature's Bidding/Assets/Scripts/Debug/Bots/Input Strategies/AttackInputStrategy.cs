using Cysharp.Threading.Tasks;

public class AttackInputStrategy : BaseDummyInputStrategy
{
    public AttackInputStrategy(PlayerContext ctx) : base(ctx) { }

    bool endAttack = false;

    protected override bool IsDone()
    {
        return endAttack;
    }

    protected override void OnStart()
    {
        endAttack = false;
        ctx.attackPressed = true;

        WaitForAttackEnd();
    }

    private async void WaitForAttackEnd()
    {
        await UniTask.WaitUntil(() => ctx.playerAttackManager.isAttacking);

        ctx.attackPressed = false;

        await UniTask.WaitUntil(() => ctx.attackOnCooldown);

        await UniTask.Delay(1000);
        
        endAttack = true;
    }

    protected override void OnEnd()
    {
        ctx.attackPressed = false;
    }
}
