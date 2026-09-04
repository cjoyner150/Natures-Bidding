using System.Collections.Generic;
using UnityEngine;

// Enum defining the mechanical purpose of a node
public enum NodeType { Fight, Shop, Tarot, Clense, Curse }

public class NodeData
{
    public int id;
    public int floorIndex;
    
    // Position as a percentage (0.0 to 1.0) across the screen. 
    // This allows width-3 floors to connect cleanly to width-100 floors.
    public float percentX; 
    
    // The final calculated world position
    public Vector2 visualPosition; 
    
    // The visual and statistical rules assigned to this specific node
    public NodeBlueprintSO blueprint;
    
    // The IDs of the nodes this node leads to on the next floor
    public List<int> connectedNodeIds = new List<int>();
}