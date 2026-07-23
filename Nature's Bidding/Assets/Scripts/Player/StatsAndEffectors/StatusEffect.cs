using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public abstract class StatusEffect
{
    public string Name;
    public StatType Stat;
    public float Value;
    public float Duration;
    protected Stats Stats { get; private set; }
    protected PlayerStatusEffectManager StatusEffectManager { get; private set; }

    public void Initialize(Stats stats, PlayerStatusEffectManager playerStatusEffectManager)
    {
        Stats = stats;
        StatusEffectManager = playerStatusEffectManager;
        OnInitialize();
    }

    public abstract StatsModifier GetStatsModifier();
    public virtual void OnInitialize() { }
    public virtual void OnTick(float delta) { }
    public virtual void OnEnd() { }

    public static Func<float, float> GetFuncByOperation(OperatorType opType, float val)
    {
        Func<float, float> func = opType == OperatorType.Multiplication ? x => x * val
            : opType == OperatorType.Addition ? x => x + val
            : opType == OperatorType.Division ? x => x / val
            : opType == OperatorType.Subtraction ? x => x - val
            : opType == OperatorType.SetEqual ? x => val
            : throw new Exception("Basic Operation should only be of a base operation type");

        return func;
    }
}