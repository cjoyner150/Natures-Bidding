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
            AssignBlueprints();
            CullUnreachableNodes();

            if (AllFloorsMeetNodeCountLimits())
            {
                validGraph = true;
                break;
            }
        }

        if (!validGraph)
        {
            var report = string.Join(", ", generatedGraph.Select((floor, i) =>
                $"F{i}:{floor.Count}/[{mapSettings.GetMinimumNodesForFloor(i)}-{mapSettings.GetMaximumNodesForFloor(i)}]"));

            GameLogger.Log(LogSeverity.Error,
                $"Map generation could not meet per-floor node limits after {MaximumGenerationAttempts} attempts. " +
                $"Last attempt after culling: {report}. Check floor widths, densities, maxConnectionDrift, and blueprint compatibility.");

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
                    blueprint = floorConfig.forcedBlueprint   // null = assigned later, with variety rules
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
        if (isSkipConnection) return; // skips always cross a zippered middle floor; leave off

        List<NodeData> sourceFloor = generatedGraph[sourceFloorIndex];
        List<NodeData> targetFloor = generatedGraph[targetFloorIndex];
        int sCount = sourceFloor.Count, tCount = targetFloor.Count;
        if (sCount == 0 || tCount == 0) return;

        // Each source's contiguous target range [lo, hi]. Ranges are non-decreasing
        // across sources, which is exactly the no-crossing condition.
        int[] lo = new int[sCount];
        int[] hi = new int[sCount];
        for (int i = 0; i < sCount; i++) { lo[i] = int.MaxValue; hi[i] = -1; }

        void Link(int s, int t)
        {
            int id = targetFloor[t].id;
            if (!sourceFloor[s].connectedNodeIds.Contains(id))
                sourceFloor[s].connectedNodeIds.Add(id);
            lo[s] = Mathf.Min(lo[s], t);
            hi[s] = Mathf.Max(hi[s], t);
        }

        // 1. Zipper: every node on both floors gets at least one link, no crossings.
        int si = 0, ti = 0;
        while (true)
        {
            Link(si, ti);
            bool lastS = si == sCount - 1, lastT = ti == tCount - 1;
            if (lastS && lastT) break;
            if (lastS) ti++;
            else if (lastT) si++;
            else if (sourceFloor[si + 1].percentX < targetFloor[ti + 1].percentX) si++;
            else ti++;
        }

        // 2. Extras: a source may extend to a neighbour's boundary target (merge/split).
        //    Processed in order with live ranges so two neighbours can't both extend into a gap.
        for (int i = 0; i < sCount; i++)
        {
            if (UnityEngine.Random.value > mapSettings.extraConnectionChance) continue;

            int desired = UnityEngine.Random.Range(mapSettings.pathsPerNodeMin, mapSettings.pathsPerNodeMax + 1);
            int current = hi[i] - lo[i] + 1;
            if (current >= desired) continue;

            var candidates = new List<int>();
            if (i > 0 && hi[i - 1] < lo[i]) candidates.Add(hi[i - 1]);              // merge into previous source's last target
            if (i < sCount - 1 && lo[i + 1] > hi[i]) candidates.Add(lo[i + 1]);     // split toward next source's first target
            if (candidates.Count == 0) continue;

            Link(i, candidates[UnityEngine.Random.Range(0, candidates.Count)]);
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

    /// <summary>
    /// Assigns blueprints top-down so that (a) a node differs from all of its parents,
    /// and (b) siblings — nodes that share any parent — differ from each other.
    /// Falls back gracefully when the pool is too small to satisfy both.
    /// </summary>
    private void AssignBlueprints()
    {
        var parentsOf = new Dictionary<int, List<NodeData>>();
        foreach (var floor in generatedGraph)
            foreach (var node in floor)
                foreach (int childId in node.connectedNodeIds)
                {
                    if (!parentsOf.TryGetValue(childId, out var list)) parentsOf[childId] = list = new List<NodeData>();
                    list.Add(node);
                }

        var exclude = new HashSet<NodeBlueprintSO>();

        foreach (var floor in generatedGraph)
        {
            foreach (var node in floor)
            {
                if (node.blueprint != null) continue; // forced by floor config

                exclude.Clear();
                if (parentsOf.TryGetValue(node.id, out var parents))
                {
                    foreach (var parent in parents)
                    {
                        if (parent.blueprint != null) exclude.Add(parent.blueprint);

                        // siblings: every other child of this parent that's already assigned
                        foreach (int siblingId in parent.connectedNodeIds)
                        {
                            if (siblingId == node.id) continue;
                            var sibling = GetNodeById(siblingId);
                            if (sibling?.blueprint != null) exclude.Add(sibling.blueprint);
                        }
                    }
                }

                node.blueprint = PickWeighted(exclude)
                              ?? PickWeighted(SiblingsOnly(node, parentsOf))
                              ?? PickWeighted(null);
            }
        }
    }

    private HashSet<NodeBlueprintSO> SiblingsOnly(NodeData node, Dictionary<int, List<NodeData>> parentsOf)
    {
        var set = new HashSet<NodeBlueprintSO>();
        if (!parentsOf.TryGetValue(node.id, out var parents)) return set;
        foreach (var parent in parents)
            foreach (int siblingId in parent.connectedNodeIds)
                if (siblingId != node.id && GetNodeById(siblingId)?.blueprint is { } bp) set.Add(bp);
        return set;
    }

    private NodeBlueprintSO PickWeighted(HashSet<NodeBlueprintSO> exclude)
    {
        float totalWeight = 0f;
        foreach (var bp in availableBlueprints)
            if (exclude == null || !exclude.Contains(bp)) totalWeight += bp.spawnWeight;
        if (totalWeight <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float acc = 0f;
        foreach (var bp in availableBlueprints)
        {
            if (exclude != null && exclude.Contains(bp)) continue;
            acc += bp.spawnWeight;
            if (roll <= acc) return bp;
        }
        return null;
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

    public bool IsFinalFloorNode(int id)
    {
        return generatedGraph.Count > 0 && generatedGraph[generatedGraph.Count - 1].Any(node => node.id == id);
    }
}