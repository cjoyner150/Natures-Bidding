using System;
using System.Collections.Generic;
using UnityEngine;

public class StatsMediator
{
    readonly LinkedList<StatsModifier> modifiers = new();

    public event EventHandler<Query> Queries;
    public void PerformQuery(object sender, Query query) => Queries?.Invoke(sender, query);

    public void AddModifier(StatsModifier modifier)
    {
        modifiers.AddLast(modifier);
        Queries += modifier.Handle;

        modifier.OnDispose += _ =>
        {
            Debug.Log($"Disposing {modifier.GetType().Name}");
            modifiers.Remove(modifier);

            string printMods = "";
            foreach (StatsModifier modifier in modifiers)
            {
                printMods += modifier.GetType().Name;
            }
            Debug.Log("new list " + printMods);

            Queries -= modifier.Handle;
        };
    }

    public void Update(float deltaTime)
    {
        // Update nodes
        var node = modifiers.First;
        while (node != null)
        {
            var modifier = node.Value;
            modifier.Update(deltaTime);
            node = node.Next;
        }

        // Dispose of nodes marked for disposal
        node = modifiers.First;
        while (node != null)
        {
            var nextNode = node.Next;
            
            if (node.Value.IsMarkedForRemoval)
            {
                node.Value.Dispose();
            }

            node = nextNode;
        }

    }
}

public class Query
{
    public StatType StatType;
    public float Value;

    public Query(StatType type, float value)
    {
        StatType = type;
        Value = value;
    }
}
