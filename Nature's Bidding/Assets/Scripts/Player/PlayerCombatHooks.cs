using System;
using System.Collections.Generic;
using System.Text;

public class PlayerCombatHooks
{
    public static event Action<long> OnParry;
    public static event Action<long> OnAttack;
    public static event Action<long> OnHit;
    public static event Action<long> OnKill;
    public static event Action<long> OnDeath;
    public static event Action<string> OnItemAdded;
    public static event Action<string> OnItemRemoved;
    public static void TriggerOnParry(long parriedTargetId) => OnParry?.Invoke(parriedTargetId);
    public static void TriggerOnAttack(long victimId) => OnAttack?.Invoke(victimId);
    public static void TriggerOnHit(long attackerId) => OnHit?.Invoke(attackerId);
    public static void TriggerOnDeath(long killCreditId) => OnDeath?.Invoke(killCreditId);
    public static void TriggerOnKill(long killedTargetId) => OnKill?.Invoke(killedTargetId);
    public static void TriggerOnItemAdded(string itemId) => OnItemAdded?.Invoke(itemId);
    public static void TriggerOnItemRemoved(string itemId) => OnItemRemoved?.Invoke(itemId);
}
