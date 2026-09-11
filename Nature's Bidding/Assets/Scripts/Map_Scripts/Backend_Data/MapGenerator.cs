using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class MapGenerator : NetworkBehaviour
{
    [Header("Data Sources")]
    public MapSettingsSO mapSettings;
    public List<NodeBlueprintSO> availableBlueprints; // Drag your blueprints here in the inspector

    // An event we will trigger when the math is done, so the MapRenderer knows to start drawing
    public event Action<List<List<NodeData>>> OnMapDataGenerated;

    // The final generated graph
    private List<List<NodeData>> generatedGraph = new List<List<NodeData>>();
    private int nextAvailableNodeId = 0;

    [Header("External Hooks")]
    [Tooltip("Check this if your external GameManager is providing the seed.")]
    public bool useExternalSeed = false;
    public int externalSeed = 0;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            int seed = 0;

            // ====================================================================
            // EXTERNAL HOOK
            // Set External seed on scene load.
            // ====================================================================
            
            if (useExternalSeed)
            {
                seed = externalSeed;
            }
            else
            {
                // Standalone debug behavior: generate a new random seed
                seed = UnityEngine.Random.Range(0, 999999);
            }
            
            GenerateMapData(seed);
            ReceiveSeedClientRpc(seed);
        }
    }

    [ClientRpc]
    private void ReceiveSeedClientRpc(int seed)
    {
        if (IsServer) return; 

        GenerateMapData(seed);
    }

    private void GenerateMapData(int seed)
    {
        // Lock the random number generator so all clients get the exact same result
        UnityEngine.Random.InitState(seed);

        generatedGraph.Clear();
        nextAvailableNodeId = 0;

        // Execute the generation steps
        PlotNodes();
        ConnectNodes();
        CullUnreachableNodes();

        // Fire the event so the visual renderer knows it can start spawning sprites
        OnMapDataGenerated?.Invoke(generatedGraph);
    }

    private void PlotNodes()
    {
        for (int f = 0; f < mapSettings.floors.Count; f++)
        {
            var floorConfig = mapSettings.floors[f];
            List<NodeData> currentFloorNodes = new List<NodeData>();

            for (int x = 0; x < floorConfig.maxWidth; x++)
            {
                // Roll for density
                // We force at least 1 node to spawn
                bool forceSpawn = (currentFloorNodes.Count == 0 && x == floorConfig.maxWidth - 1);

                if (UnityEngine.Random.value <= floorConfig.nodeDensity || forceSpawn)
                {
                    // Calculate percentage across the screen (0.0 to 1.0)
                    float percent = 0.5f; 
                    if (floorConfig.maxWidth > 1)
                    {
                        percent = (float)x / (floorConfig.maxWidth - 1);
                    }

                    NodeData newNode = new NodeData
                    {
                        id = nextAvailableNodeId++,
                        floorIndex = f,
                        percentX = percent,
                        blueprint = GetBlueprintForNode(floorConfig)
                    };

                    currentFloorNodes.Add(newNode);
                }
            }
            generatedGraph.Add(currentFloorNodes);
        }
    }

    private void ConnectNodes()
    {
        // Loop from the bottom floor up to the second-to-last floor
        for (int f = 0; f < generatedGraph.Count - 1; f++)
        {
            List<NodeData> currentFloor = generatedGraph[f];
            List<NodeData> nextFloor = generatedGraph[f + 1];

            int lastTargetIndex = 0; // Prevents lines from crossing

            for (int i = 0; i < currentFloor.Count; i++)
            {
                NodeData node = currentFloor[i];
                List<int> validTargetIndices = new List<int>();

                // Find valid nodes on the next floor based on percentage drift and no-crossing rule
                for (int j = lastTargetIndex; j < nextFloor.Count; j++)
                {
                    NodeData targetNode = nextFloor[j];
                    
                    // Check if it's within reach (e.g., node at 50% can reach 30% to 70%)
                    if (Mathf.Abs(targetNode.percentX - node.percentX) <= mapSettings.maxConnectionDrift)
                    {
                        validTargetIndices.Add(j);
                    }
                }

                // Fallback: If map drift rules were too strict, force connect to the closest legal node
                if (validTargetIndices.Count == 0 && lastTargetIndex < nextFloor.Count)
                {
                    validTargetIndices.Add(lastTargetIndex);
                }

                if (validTargetIndices.Count > 0)
                {
                    // Determine how many branches this node will shoot out
                    int numConnections = UnityEngine.Random.Range(mapSettings.pathsPerNodeMin, mapSettings.pathsPerNodeMax + 1);
                    numConnections = Mathf.Min(numConnections, validTargetIndices.Count);

                    // To prevent crossing, if we pick target B and C, the NEXT node on this floor can only connect to C or D. 
                    // It cannot reach backwards to A or B.
                    int highestTargetIndexPicked = lastTargetIndex;

                    // Shuffle our valid targets to pick random connections, then sort them to maintain logic
                    var shuffledTargets = validTargetIndices.OrderBy(x => UnityEngine.Random.value).Take(numConnections).ToList();
                    shuffledTargets.Sort(); 

                    foreach (int targetIdx in shuffledTargets)
                    {
                        node.connectedNodeIds.Add(nextFloor[targetIdx].id);
                        if (targetIdx > highestTargetIndexPicked) highestTargetIndexPicked = targetIdx;
                    }

                    // Update the no-cross tracking for the next node on this floor
                    lastTargetIndex = highestTargetIndexPicked;
                }
            }
        }
    }

    private void CullUnreachableNodes()
    {
        // Nodes that don't get connected from the floor below are "orphans" and shouldn't exist.
        // We do a reachability pass starting from floor 0.
        
        HashSet<int> reachableNodeIds = new HashSet<int>();
        
        // Floor 0 is always reachable
        foreach (var node in generatedGraph[0]) 
        {
            reachableNodeIds.Add(node.id);
        }

        // Trace paths upwards
        for (int f = 0; f < generatedGraph.Count - 1; f++)
        {
            foreach (var node in generatedGraph[f])
            {
                if (reachableNodeIds.Contains(node.id))
                {
                    foreach (var connectionId in node.connectedNodeIds)
                    {
                        reachableNodeIds.Add(connectionId);
                    }
                }
            }
        }

        // Delete any node that is not in the reachable set (skipping floor 0)
        for (int f = 1; f < generatedGraph.Count; f++)
        {
            generatedGraph[f].RemoveAll(node => !reachableNodeIds.Contains(node.id));
        }
    }

    private NodeBlueprintSO GetBlueprintForNode(MapSettingsSO.FloorConfig config)
    {
        // Check if the settings enforce a specific node here
        if (config.forcedBlueprint != null) return config.forcedBlueprint;

        //  Otherwise, use Weighted Random Generation
        float totalWeight = 0f;
        foreach (var bp in availableBlueprints) totalWeight += bp.spawnWeight;

        float randomVal = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var bp in availableBlueprints)
        {
            currentWeight += bp.spawnWeight;
            if (randomVal <= currentWeight)
            {
                return bp;
            }
        }

        // Fallback in case of floating point rounding errors
        return availableBlueprints[0]; 
    }
}