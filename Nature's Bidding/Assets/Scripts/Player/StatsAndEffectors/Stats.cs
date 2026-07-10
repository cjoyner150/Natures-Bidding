using System;

public enum StatType 
{ 
    MaxHealth, 
    Damage, 
    AttackSpeed, 
    MoveSpeed,
    Jumps,
    Size,
    KnockbackResistance,
    ParryDuration, 
    ParryCooldown,
    DashDistance,
    DashCooldown,
    CritChance,
    CritDamageMultiplier,
    Momentum,
    ComboDamage,
    Stealing,
    Lifesteal,
    Gold
}

public class Stats
{
    readonly StatsMediator mediator;
    readonly BasePlayerStats baseStats;
    public readonly PersistentPlayerData playerData;

    public StatsMediator Mediator => mediator;

    public Stats(StatsMediator mediator, BasePlayerStats baseStats, PersistentPlayerData playerData)
    {
        this.playerData = playerData;
        this.mediator=mediator;
        this.baseStats=baseStats;
    }

    public override string ToString()
    {
        return $"MaxHealth: {MaxHealth}, Damage: {Damage}, AttackSpeed: {AttackSpeed}, " +
               $"MoveSpeed: {MoveSpeed}, Jumps: {Jumps}, Size: {Size}, " +
               $"KnockbackResistance: {KnockbackResistance}, ParryDuration: {ParryDuration}, ParryCooldown: {ParryCooldown}, " +
               $"DashDistance: {DashDistance}, DashCooldown: {DashCooldown}, CritChance: {CritChance}, " +
               $"CritDamageMultiplier: {CritDamageMultiplier}, Momentum: {Momentum}, " +
               $"ComboMultiplier: {ComboDamage}, Stealing: {Stealing}, Lifesteal: {Lifesteal}, Gold: {playerData.gold}";
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

    public float Jumps
    {
        get
        {
            var q = new Query(StatType.Jumps, baseStats.Jumps);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }

    public float Size
    {
        get
        {
            var q = new Query(StatType.Size, baseStats.Size);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }

    public float KnockbackResistance
    {
        get
        {
            var q = new Query(StatType.KnockbackResistance, baseStats.KnockbackResistance);
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
    
    public float ComboDamage
    {
        get
        {
            var q = new Query(StatType.ComboDamage, baseStats.ComboDamage);
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
    
    public float Lifesteal
    {
        get
        {
            var q = new Query(StatType.Lifesteal, baseStats.Lifesteal);
            mediator.PerformQuery(this, q);
            return q.Value;
        }
    }

    public float GetStatByType(StatType type)
    {
        return type == StatType.MaxHealth ? MaxHealth
            : type == StatType.Damage ? Damage
            : type == StatType.AttackSpeed ? AttackSpeed
            : type == StatType.MoveSpeed ? MoveSpeed
            : type == StatType.Jumps ? Jumps
            : type == StatType.Size ? Size
            : type == StatType.KnockbackResistance ? KnockbackResistance
            : type == StatType.ParryDuration ? ParryDuration
            : type == StatType.ParryCooldown ? ParryCooldown
            : type == StatType.DashDistance ? DashDistance
            : type == StatType.DashCooldown ? DashCooldown
            : type == StatType.CritChance ? CritChance
            : type == StatType.CritDamageMultiplier ? CritDamageMultiplier
            : type == StatType.Momentum ? Momentum
            : type == StatType.ComboDamage ? ComboDamage
            : type == StatType.Stealing ? Stealing
            : type == StatType.Lifesteal ? Lifesteal
            : type == StatType.Gold ? playerData.gold
            : throw new Exception($"Unhandled StatType: {type}");
    }
}