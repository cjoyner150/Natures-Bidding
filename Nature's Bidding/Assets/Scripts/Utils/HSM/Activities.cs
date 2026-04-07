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

    public class RetainKnockbackOnUnexpectedTransition : Activity
    {
        
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
