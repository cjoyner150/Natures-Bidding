using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HSM;
using UnityEngine;

public class AirborneStunned: State
{
    private readonly PlayerContext ctx;
    bool exitStunned;

    public AirborneStunned(StateMachine machine, PlayerContext ctx, State parent) : base(machine, parent)
    {
        this.ctx = ctx;
    }

    protected override void OnEnter()
    {
        ctx.anim.SetBool("Stunned", true);

        ctx.isStunned = true;
        ctx.desiredMaxSpeed = 0;

        ctx.forceToAdd = Vector3.zero;
        ctx.rb.linearVelocity = new Vector3(0, ctx.rb.linearVelocity.y, 0);

        exitStunned = false;
        ctx.stunRecoveryTimer = ctx.stunTime;
        ctx.shouldStunSelf = false;
    }

    protected override void OnUpdate(float deltaTime)
    {
        // If the player receives a parry response late, that means the rpc was delayed but the attacking player was parried, so we should not let the player be stunned. 
        if (ctx.parryResponse)
        {
            exitStunned = true;
            ctx.parryResponse = false;
        }

        if (ctx.stunRecoveryTimer > 0) ctx.stunRecoveryTimer -= deltaTime;
        else if (ctx.additionalStunTime > 0) ctx.additionalStunTime -= deltaTime;

        if (ctx.stunRecoveryTimer <= 0 && ctx.additionalStunTime <= 0)
        {
            exitStunned = true;
        }

        if (ctx.shouldTakeKnockback) exitStunned = true;
    }

    protected override void OnExit()
    {
        if (exitStunned || ctx.shouldTakeKnockback)
        {
            ctx.shouldStunSelf = false;
            ctx.isStunned = false;
            exitStunned = false;
            ctx.anim.SetBool("Stunned", false);
            ctx.playerHealth.isStunned.Value = false;
        }
    }

    protected override State GetTransition()
    {
        if (exitStunned) return GetParentOfType<Fall>().fallLocomotion;
        else return null;
    }
}

