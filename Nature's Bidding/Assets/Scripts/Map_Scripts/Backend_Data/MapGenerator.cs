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

    // In-scene NetworkObjects spawn (and can generate) before other scene MonoBehaviours run Start(),
    // so late subscribers need to be able to pull the already-generated graph instead of missing the event.
    public bool HasGeneratedData { get; private set; }
    public List<List<NodeData>> CurrentGraph => generatedGraph;

    // The final generated graph
    private List<List<NodeData>> generatedGraph = new List<List<NodeData>>();
    private int nextAvailableNodeId = 0;
    private const int MaximumGenerationAttempts = 1000;

    [Header("External Hooks")]
    [Tooltip("Check this if your external GameManager is providing the seed.")]
    public bool useExternalSeed = false;
    public int externalSeed = 0;

    public override void OnNetworkSpawn()
    {
        GameLogger.Log(LogSeverity.Debug, $"MapGenerator.OnNetworkSpawn called. IsServer={IsServer}, IsClient={IsClient}");
        if (IsServer)
        {
            int seed;

            // ====================================================================
            // EXTERNAL HOOK
            // Set External seed on scene load.
            // ====================================================================

            if (PersistentGameStateManager.Instance != null)
            {
                seed = PersistentGameStateManager.Instance.RequestMapSeed();
            }
            else if (useExternalSeed)
            {
                seed = externalSeed;
            }
            else
            {
                // Standalone debug behavior: generate a new random seed
                seed = UnityEngine.Random.Range(0, 999999);
            }
            
            int generatedSeed = GenerateMapData(seed);
            ReceiveSeedClientRpc(generatedSeed);
        }
    }

    [ClientRpc]
    private void ReceiveSeedClientRpc(int seed)
    {
        if (IsServer) return; 

        GenerateMapData(seed);
    }

    private int GenerateMapData(int seed)
    {
        GameLogger.Log(LogSeverity.Debug, $"MapGenerator.GenerateMapData called with seed={seed}, mapSettings null={mapSettings == null}, availableBlueprints count={availableBlueprints?.Count ?? -1}");
        if (!CanSatisfyNodeCountLimits())
            return seed;

        int generatedSeed = seed;
        bool validGraph = false;
        for (int attempt = 0; attempt < MaximumGenerationAttempts; attempt++)
        {
            generatedSeed = unchecked(seed + attempt);
            UnityEngine.Random.InitState(generatedSeed);

            generatedGraph.Clear();
            nextAvailableNodeId = 0;

            PlotNodes();
            ConnectNodes();
            CullUnreachableNodes();

            if (AllFloorsMeetNodeCountLimits())
            {
                validGraph = true;
                break;
            }
        }

        if (!validGraph)
        {
            GameLogger.Log(LogSeverity.Error,
                $"Map generation could not meet the configured per-floor node limits after {MaximumGenerationAttempts} attempts. Check floor widths, densities, and blueprint compatibility.");
            generatedGraph.Clear();
            HasGeneratedData = false;
            return seed;
        }

        // Fire the event so the visual renderer knows it can start spawning sprites
        HasGeneratedData = true;
        GameLogger.Log(LogSeverity.Debug, $"MapGenerator.GenerateMapData: generated {generatedGraph.Count} floors ({generatedGraph.Sum(f => f.Count)} nodes). Invoking OnMapDataGenerated (has listeners={OnMapDataGenerated != null}).");
        OnMapDataGenerated?.Invoke(generatedGraph);
        return generatedSeed;
    }

    private bool CanSatisfyNodeCountLimits()
    {
        if (mapSettings == null || mapSettings.floors == null || mapSettings.floors.Count == 0)
        {
            GameLogger.Log(LogSeverity.Error, "Map generation requires map settings with at least one floor.");
            return false;
        }

        if (mapSettings.minNodesPerFloor < 1 || mapSettings.maxNodesPerFloor < mapSettings.minNodesPerFloor)
        {
            GameLogger.Log(LogSeverity.Error, "Map settings require maxNodesPerFloor to be at least minNodesPerFloor, and minNodesPerFloor to be at least 1.");
            return false;
        }

        for (int floorIndex = 0; floorIndex < mapSettings.floors.Count; floorIndex++)
        {
            int minimum = mapSettings.GetMinimumNodesForFloor(floorIndex);
            int maximum = mapSettings.GetMaximumNodesForFloor(floorIndex);
            int capacity = Mathf.Min(mapSettings.floors[floorIndex].maxWidth, maximum);
            if (minimum < 1 || maximum < minimum)
            {
                GameLogger.Log(LogSeverity.Error,
                    $"Floor {floorIndex} has invalid node limits: minimum={minimum}, maximum={maximum}.");
                return false;
            }

            if (mapSettings.floors[floorIndex].maxWidth < 1 || capacity < minimum)
            {
                GameLogger.Log(LogSeverity.Error,
                    $"Floor {floorIndex} cannot fit its minimum of {minimum} nodes. Its maxWidth is {mapSettings.floors[floorIndex].maxWidth} and its effective maximum is {maximum}.");
                return false;
            }

            if (mapSettings.floors[floorIndex].nodeDensity <= 0f && minimum > 1)
            {
                GameLogger.Log(LogSeverity.Error,
                    $"Floor {floorIndex} has zero node density, so it cannot randomly generate its required minimum of {minimum} nodes.");
                return false;
            }
        }

        return true;
    }

    private bool AllFloorsMeetNodeCountLimits()
    {
        for (int floorIndex = 0; floorIndex < generatedGraph.Count; floorIndex++)
        {
            List<NodeData> floor = generatedGraph[floorIndex];
            if (floor.Count < mapSettings.GetMinimumNodesForFloor(floorIndex) ||
                floor.Count > mapSettings.GetMaximumNodesForFloor(floorIndex))
                return false;
        }

        return generatedGraph.Count == mapSettings.floors.Count;
    }

    private void PlotNodes()
    {
        for (int f = 0; f < mapSettings.floors.Count; f++)
        {
            var floorConfig = mapSettings.floors[f];
            List<NodeData> currentFloorNodes = new List<NodeData>();
            int nodeLimit = Mathf.Min(floorConfig.maxWidth, mapSettings.GetMaximumNodesForFloor(f));

            List<int> selectedSlots = new List<int>();

            for (int x = 0; x < floorConfig.maxWidth; x++)
            {
                if (UnityEngine.Random.value <= floorConfig.nodeDensity)
                    selectedSlots.Add(x);
            }

            if (selectedSlots.Count == 0 && floorConfig.maxWidth > 0)
                selectedSlots.Add(UnityEngine.Random.Range(0, floorConfig.maxWidth));

            if (selectedSlots.Count > nodeLimit)
            {
                selectedSlots = selectedSlots
                    .OrderBy(_ => UnityEngine.Random.value)
                    .Take(nodeLimit)
                    .OrderBy(slot => slot)
                    .ToList();
            }

            foreach (int x in selectedSlots)
            {
                float percent = floorConfig.maxWidth > 1
                    ? (float)x / (floorConfig.maxWidth - 1)
                    : 0.5f;

                currentFloorNodes.Add(new NodeData
                {
                    id = nextAvailableNodeId++,
                    floorIndex = f,
                    percentX = percent,
                    blueprint = GetBlueprintForNode(floorConfig)
                });
            }

            generatedGraph.Add(currentFloorNodes);
        }
    }

    private void ConnectNodes()
    {
        for (int f = 0; f < generatedGraph.Count - 1; f++)
        {
            ConnectFloorPair(f, f + 1, false);
        }

        for (int f = 0; f < generatedGraph.Count - 2; f++)
        {
            ConnectFloorPair(f, f + 2, true);
        }
    }

    private void ConnectFloorPair(int sourceFloorIndex, int targetFloorIndex, bool isSkipConnection)
    {
        List<NodeData> sourceFloor = generatedGraph[sourceFloorIndex];
        List<NodeData> targetFloor = generatedGraph[targetFloorIndex];
        int lastTargetIndex = 0;

        for (int i = 0; i < sourceFloor.Count; i++)
        {
            NodeData node = sourceFloor[i];
            if (isSkipConnection && UnityEngine.Random.value > mapSettings.skipFloorConnectionChance)
                continue;

            List<int> validTargetIndices = new List<int>();

            // Keep links ordered to avoid crossing, and prohibit reusing the same blueprint asset.
            for (int j = lastTargetIndex; j < targetFloor.Count; j++)
            {
                NodeData targetNode = targetFloor[j];
                if (targetNode.blueprint == node.blueprint)
                    continue;

                if (Mathf.Abs(targetNode.percentX - node.percentX) <= mapSettings.maxConnectionDrift)
                {
                    validTargetIndices.Add(j);
                }
            }

            // If drift rules are too strict, use the closest compatible target without allowing crossings.
            if (validTargetIndices.Count == 0)
            {
                int closestCompatibleIndex = -1;
                float closestDistance = float.MaxValue;
                for (int j = lastTargetIndex; j < targetFloor.Count; j++)
                {
                    NodeData targetNode = targetFloor[j];
                    if (targetNode.blueprint == node.blueprint)
                        continue;

                    float distance = Mathf.Abs(targetNode.percentX - node.percentX);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestCompatibleIndex = j;
                    }
                }

                if (closestCompatibleIndex >= 0)
                    validTargetIndices.Add(closestCompatibleIndex);
            }

            if (validTargetIndices.Count == 0)
                continue;

            int numConnections = isSkipConnection
                ? 1
                : UnityEngine.Random.Range(mapSettings.pathsPerNodeMin, mapSettings.pathsPerNodeMax + 1);
            numConnections = Mathf.Min(numConnections, validTargetIndices.Count);

            var shuffledTargets = validTargetIndices
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(numConnections)
                .OrderBy(index => index)
                .ToList();

            foreach (int targetIndex in shuffledTargets)
                node.connectedNodeIds.Add(targetFloor[targetIndex].id);

            lastTargetIndex = shuffledTargets[shuffledTargets.Count - 1];
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

        // Keep only nodes that can continue to the final floor.
        HashSet<int> reachesFinalFloor = new HashSet<int>();
        foreach (NodeData node in generatedGraph[generatedGraph.Count - 1])
            reachesFinalFloor.Add(node.id);

        for (int f = generatedGraph.Count - 2; f >= 0; f--)
        {
            foreach (NodeData node in generatedGraph[f])
            {
                if (node.connectedNodeIds.Any(reachesFinalFloor.Contains))
                    reachesFinalFloor.Add(node.id);
            }
        }

        foreach (List<NodeData> floor in generatedGraph)
            floor.RemoveAll(node => !reachesFinalFloor.Contains(node.id));
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

    public NodeData GetNodeById(int id)
    {
        foreach (var floor in generatedGraph)
        {
            foreach (var node in floor)
            {
                if (node.id == id) return node;
            }
        }

        return null;
    }

    public bool IsFloorZeroNode(int id)
    {
        return generatedGraph.Count > 0 && generatedGraph[0].Any(node => node.id == id);
    }
}