using UnityEngine;
using HSM;
using Cysharp.Threading.Tasks;

public class Parry : State
{
    private readonly PlayerContext ctx;
    
    private bool exitParry;
    private float parryTimer;

    public Parry(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.desiredMaxSpeed = 0;
        ctx.forceToAdd = Vector3.zero;

        ctx.anim.SetTrigger("Parry");

        SetParryActive(ctx.parryWarmUpDelay);

        parryTimer = ctx.playerStats.ParryDuration;
        exitParry = false;

    }

    async void SetParryActive(int delay)
    {
        await UniTask.Delay(delay);
        ctx.playerHealth.BeginParry();
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (ctx.parryResponse)
        {
            exitParry = true;
            return;
        }

        parryTimer -= deltaTime;
        if (parryTimer <= 0) exitParry = true;
    }

    protected override void OnExit()
    {
        ctx.forceToAdd = Vector3.zero;
        ctx.parryCDTimer = ctx.playerStats.ParryCooldown;

        ctx.playerHealth.EndParry();

        ctx.parryResponse = false;
    }

    protected override State GetTransition() 
    {
        if (exitParry) return GetParentOfType<Grounded>().groundedLocomotion;
        else return null;
    }

    //[Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    //public void PlayerParryFeedbackClientRpc(Vector3 fromPosition)
    //{
    //    PlayerVisualEffectManager.SpawnParryEffectsOnPlayer?.Invoke(OwnerClientId);
    //}
}