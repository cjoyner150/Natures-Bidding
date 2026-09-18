using System;
using System.Collections.Generic;
using System.Text;

public class PlayerCombatHooks
{
    public static event Action<ulong> OnParry;
    public static event Action<ulong> OnAttack;
    public static event Action<ulong> OnHit;
    public static event Action<ulong> OnKill;
    public static event Action<ulong> OnDeath;
    public static event Action<string> OnItemAdded;
    public static event Action<string> OnItemRemoved;
    public static void TriggerOnParry(ulong parriedTargetId) => OnParry?.Invoke(parriedTargetId);
    public static void TriggerOnAttack(ulong victimId) => OnAttack?.Invoke(victimId);
    public static void TriggerOnHit(ulong attackerId) => OnHit?.Invoke(attackerId);
    public static void TriggerOnDeath(ulong killCreditId) => OnDeath?.Invoke(killCreditId);
    public static void TriggerOnKill(ulong killedTargetId) => OnKill?.Invoke(killedTargetId);
    public static void TriggerOnItemAdded(string itemId) => OnItemAdded?.Invoke(itemId);
    public static void TriggerOnItemRemoved(string itemId) => OnItemRemoved?.Invoke(itemId);
}
