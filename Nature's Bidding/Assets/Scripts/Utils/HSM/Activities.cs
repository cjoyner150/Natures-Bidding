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


    public class PauseInAirActivity : Activity
    {
        PlayerContext ctx;
        float seconds;

        public PauseInAirActivity(PlayerContext ctx, float seconds)
        {
            this.ctx = ctx;
            this.seconds = seconds;
        }

        public override async Task ActivateAsync(CancellationToken ct)
        {
            ctx.rb.useGravity = false;
            ctx.rb.linearVelocity = Vector3.zero;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            }
            catch (OperationCanceledException)
            {
                ctx.rb.useGravity = true;
                throw;
            }
            await base.ActivateAsync(ct);
        }

        //public override async Task DeactivateAsync(CancellationToken ct)
        //{
        //    ctx.rb.linearVelocity = Vector3.zero;
        //    await base.DeactivateAsync(ct);
        //    await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
        //    ctx.rb.useGravity = true;
        //}
    }

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
