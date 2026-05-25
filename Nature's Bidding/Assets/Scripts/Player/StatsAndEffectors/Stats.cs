using System;

public enum StatType 
{ 
    MaxHealth, 
    Damage, 
    AttackSpeed, 
    MoveSpeed, 
    ParryDuration, 
    ParryCooldown,
    DashDistance,
    DashCooldown,
    CritChance,
    CritDamageMultiplier,
    Momentum,
    ComboMultiplier,
    Stealing
}

public class Stats
{
    readonly StatsMediator mediator;
    readonly BasePlayerStats baseStats;

    public StatsMediator Mediator => mediator;

    public Stats(StatsMediator mediator, BasePlayerStats baseStats)
    {
        this.mediator=mediator;
        this.baseStats=baseStats;
    }

    public override string ToString()
    {
        return $"MaxHealth: {MaxHealth}, Damage: {Damage}, AttackSpeed: {AttackSpeed}, " +
               $"MoveSpeed: {MoveSpeed}, ParryDuration: {ParryDuration}, ParryCooldown: {ParryCooldown}, " +
               $"DashDistance: {DashDistance}, DashCooldown: {DashCooldown}, CritChance: {CritChance}, " +
               $"CritDamageMultiplier: {CritDamageMultiplier}, Momentum: {Momentum}, " +
               $"ComboMultiplier: {ComboMultiplier}, Stealing: {Stealing}";
    }

    public float MaxHealth
    {
        get
        {
            var q = new Query(StatType.MaxHealth, baseStats.MaxHealth);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float Damage
    {
        get
        {
            var q = new Query(StatType.Damage, baseStats.Damage);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float AttackSpeed
    {
        get
        {
            var q = new Query(StatType.AttackSpeed, baseStats.AttackSpeed);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float MoveSpeed
    {
        get
        {
            var q = new Query(StatType.MoveSpeed, baseStats.MoveSpeed);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float ParryDuration
    {
        get
        {
            var q = new Query(StatType.ParryDuration, baseStats.ParryDuration);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float ParryCooldown
    {
        get
        {
            var q = new Query(StatType.ParryCooldown, baseStats.ParryCooldown);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float DashDistance
    {
        get
        {
            var q = new Query(StatType.DashDistance, baseStats.DashDistance);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float DashCooldown
    {
        get
        {
            var q = new Query(StatType.DashCooldown, baseStats.DashCooldown);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float CritChance
    {
        get
        {
            var q = new Query(StatType.CritChance, baseStats.CritChance);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float CritDamageMultiplier
    {
        get
        {
            var q = new Query(StatType.CritDamageMultiplier, baseStats.CritDamageMultiplier);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float Momentum
    {
        get
        {
            var q = new Query(StatType.Momentum, baseStats.Momentum);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float ComboMultiplier
    {
        get
        {
            var q = new Query(StatType.ComboMultiplier, baseStats.ComboMultiplier);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
    
    public float Stealing
    {
        get
        {
            var q = new Query(StatType.Stealing, baseStats.Stealing);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }
}