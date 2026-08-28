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
        ctx.rb.linearVelocity = Vector3.zero;
        ctx.desiredMaxSpeed = 0;
        ctx.forceToAdd = Vector3.zero;

        ctx.anim.SetTrigger("Parry");
        NetworkVisualEffectManager.SpawnParryEffectsOnPlayer?.Invoke(ctx.playerHealth.OwnerClientId, (int)(ctx.playerStats.ParryDuration * 1000));

        ctx.playerHealth.BeginParry();
        parryTimer = ctx.playerStats.ParryDuration;

        exitParry = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (ctx.parryResponse)
        {
            ctx.anim.SetTrigger("ParryEnd");
            exitParry = true;
            return;
        }

        parryTimer -= deltaTime;
        if (parryTimer <= 0) 
        {
            ctx.shouldStunSelf = true;
            exitParry = true; 
        }
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
        if (exitParry) return GetParentOfType<Grounded>().groundedLocomotion.idle; 
        else return null;
    }

}