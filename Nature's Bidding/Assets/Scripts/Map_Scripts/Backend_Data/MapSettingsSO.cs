using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewMapSettings", menuName = "Map/Map Settings")]
public class MapSettingsSO : ScriptableObject
{
    public enum MapOrientation { BottomToTop, LeftToRight }
    
    [Header("Global Settings")]
    public MapOrientation orientation = MapOrientation.BottomToTop;
    
    [Header("Quick Setup")]
    [Tooltip("Change this number to auto-generate the floor list below!")]
    [Min(1)] 
    public int totalFloors = 15; 

    [System.Serializable] 
    public class FloorConfig
    {
        [HideInInspector] 
        public string inspectorName;
        
        [Tooltip("Max capacity of nodes on this floor")]
        public int maxWidth;
        
        [Tooltip("Chance (0.0 - 1.0) for a valid slot to actually spawn a node")]
        [Range(0f, 1f)] 
        public float nodeDensity;
        
        [Tooltip("If assigned, EVERY node on this floor will be this type (e.g., Boss). Leave empty for random.")]
        public NodeBlueprintSO forcedBlueprint; 
    }

    [Header("Floor Details")]
    public List<FloorConfig> floors = new List<FloorConfig>();

    [Header("Pathing Rules")]
    public int pathsPerNodeMin = 1;
    public int pathsPerNodeMax = 3;
    
    [Tooltip("How far left/right a connection can reach. 0.3 means a node at 50% X can connect to nodes between 20% and 80% X on the next floor.")]
    [Range(0.1f, 1f)]
    public float maxConnectionDrift = 0.4f;

    
    private void OnValidate()
    {
        if (floors == null) floors = new List<FloorConfig>();

        // Add new floors if the total increased
        while (floors.Count < totalFloors)
        {
            floors.Add(new FloorConfig
            {
                inspectorName = "Floor " + floors.Count,
                maxWidth = Random.Range(3, 7),
                nodeDensity = 0.8f,
                forcedBlueprint = null
            });
        }

        // Remove extra floors if the total decreased
        if (floors.Count > totalFloors)
        {
            floors.RemoveRange(totalFloors, floors.Count - totalFloors);
        }

        // Update list names
        for (int i = 0; i < floors.Count; i++)
        {
            floors[i].inspectorName = "Floor " + i;
        }
    }
}