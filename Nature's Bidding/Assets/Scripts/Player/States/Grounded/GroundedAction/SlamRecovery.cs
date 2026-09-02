using HSM;
using UnityEngine;

/// <summary>
/// Entered on ground impact from FallAttack. OnEnter locks the player in place;
/// the DelayActivationActivity then holds the whole state machine frozen (no
/// OnUpdate/GetTransition runs for ANY state while an activation phase is in
/// flight) for the recovery duration. The instant it unfreezes, GetTransition
/// fires immediately into normal grounded locomotion.
/// </summary>
public class SlamRecovery : State
{
    private readonly PlayerContext ctx;

    public SlamRecovery(StateMachine machine, PlayerContext ctx, State parent = null) : base(machine, parent)
    {
        this.ctx = ctx;
        Add(new DelayActivationActivity(.5f));
    }

    protected override void OnEnter()
    {
        ctx.forceToAdd = Vector3.zero;
        ctx.desiredMaxSpeed = 0f;
        ctx.rb.linearVelocity = new Vector3(0f, ctx.rb.linearVelocity.y, 0f);
    }

    protected override State GetTransition()
    {
        return GetParentOfType<Grounded>().groundedLocomotion;
    }
}