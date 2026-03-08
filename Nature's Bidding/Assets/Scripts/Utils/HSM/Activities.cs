using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace HSM
{
    public enum ActivityMode { Inactive, Activating, Active, Deactivating }

    public interface IActivity
    {
        ActivityMode Mode { get; }
        Task ActivateAsync(CancellationToken ct);
        Task DeactivateAsync(CancellationToken ct);
    }


    //public class JumpActivity : Activity
    //{
    //    public PlayerContext ctx;
    //    public float jumpTime;

    //    public override Task ActivateAsync(CancellationToken ct)
    //    {
    //        return base.ActivateAsync(ct);
    //    }

    //    public override async Task DeactivateAsync(CancellationToken ct)
    //    {
    //        Mode = ActivityMode.Deactivating;

    //        if (ctx.jumpPressed)
    //        {

    //            ctx.jumpPressed = false;

    //            ctx.velocity = ctx.anim.velocity;
    //            ctx.velocity.y = Mathf.Sqrt(2 * ctx.gravity * ctx.jumpHeight);

    //            ctx.anim.SetBool("Sprinting", ctx.sprintPressed);
    //            ctx.anim.SetBool("Moving", ctx.isMoving);
    //            ctx.anim.SetTrigger("Jump");
    //            ctx.isGrounded = false;

    //            await UpdateJumpMotionForTime(jumpTime, ctx.sprintPressed, ct);
    //        }

    //        Mode = ActivityMode.Inactive;
    //    }

    //    async UniTask UpdateJumpMotionForTime(float time, bool sprinting, CancellationToken ct)
    //    {
    //        float t = time;

    //        while (t > 0)
    //        {
    //            t -= Time.deltaTime;

    //            Vector3 move = sprinting ? ctx.rootMotion : (ctx.rootMotion * .5f);

    //            ctx.velocity.y -= ctx.gravity * Time.deltaTime;
    //            ctx.velocity += ctx.moveDirection * ctx.airMultiplier * Time.deltaTime;

    //            ctx.cc.Move((ctx.velocity * Time.deltaTime) + move);
    //            ctx.rootMotion = Vector3.zero;

    //            if (t < time * .5f)
    //            {
    //                ctx.isGrounded = ctx.cc.isGrounded;
    //                if (ctx.isGrounded) break;
    //            }


    //            await UniTask.NextFrame();
    //        }
    //    }
    //}

    public class DelayActivationActivity : Activity
    {
        public float seconds = 0.2f;

        public override async Task ActivateAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            await base.ActivateAsync(ct);
        }
    }

    public abstract class Activity : IActivity
    {
        public ActivityMode Mode { get; protected set; } = ActivityMode.Inactive;

        public virtual async Task ActivateAsync(CancellationToken ct)
        {
            if (Mode != ActivityMode.Inactive) return;

            Mode = ActivityMode.Activating;
            await Task.CompletedTask;
            Mode = ActivityMode.Active;
            Debug.Log($"Activated {GetType().Name} (mode={Mode})");
        }

        public virtual async Task DeactivateAsync(CancellationToken ct)
        {
            if (Mode != ActivityMode.Active) return;

            Mode = ActivityMode.Deactivating;
            await Task.CompletedTask;
            Mode = ActivityMode.Inactive;
            Debug.Log($"Deactivated {GetType().Name} (mode={Mode})");
        }
    }
}
