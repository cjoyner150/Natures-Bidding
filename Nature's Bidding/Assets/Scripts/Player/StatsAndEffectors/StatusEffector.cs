using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public enum OperatorType { Addition, Multiplication, Division, Subtraction}

public abstract class StatusEffectorSO : ScriptableObject
{
    public string Name;
    public string Description;
    public float Duration = -1;
    public abstract List<StatusEffect> GetStatusEffects();
}



