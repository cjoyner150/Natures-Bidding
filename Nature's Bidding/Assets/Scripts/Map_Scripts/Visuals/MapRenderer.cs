using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapRenderer : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public MapLineDrawer lineDrawer;
    public GameObject nodePrefab; // Needs a SpriteRenderer and CircleCollider2D
    public GameObject decoratorPrefab; // Simple prefab with just a SpriteRenderer

    [Header("Layout Settings")]
    [Tooltip("How wide the map spreads out horizontally.")]
    public float mapWidthMultiplier = 10f;
    [Tooltip("Vertical distance between floors.")]
    public float floorSpacingY = 2.5f;
    public float targetNodeSize = 1.5f;
    [Tooltip("Minimum world-space radius of each node's click target.")]
    public float minimumNodeClickRadius = 1f;
    [Tooltip("Duration of the return-to-node camera movement.")]
    [SerializeField] private float returnFocusDuration = 0.7f;

    [Header("Organic Jitter")]
    public Vector2 maxJitter = new Vector2(0.3f, 0.3f);

    // Keep track of spawned objects to clean up if we reroll the map
    private List<GameObject> spawnedVisuals = new List<GameObject>();
    private MapVotingManager votingManager;
    private MapCameraController mapCameraController;
    private int focusedNodeId = int.MinValue;

    private void Start()
    {
        GameLogger.Log(LogSeverity.Debug, $"MapRenderer.Start: subscribing to mapGenerator (null={mapGenerator == null})");
        // Subscribe to the generator's completion event
        mapGenerator.OnMapDataGenerated += DrawMap;
        votingManager = FindFirstObjectByType<MapVotingManager>();
        mapCameraController = FindFirstObjectByType<MapCameraController>();
        if (votingManager != null)
            votingManager.CurrentNodeId.OnValueChanged += OnCurrentNodeChanged;

        // The generator's NetworkObject can spawn (and generate) before this Start() runs, so pull any already-generated data.
        if (mapGenerator.HasGeneratedData)
            DrawMap(mapGenerator.CurrentGraph);
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
        {
            mapGenerator.OnMapDataGenerated -= DrawMap;
        }

        if (votingManager != null)
            votingManager.CurrentNodeId.OnValueChanged -= OnCurrentNodeChanged;
    }

    private void DrawMap(List<List<NodeData>> graph)
    {
        GameLogger.Log(LogSeverity.Debug, $"MapRenderer.DrawMap called with {graph.Count} floors, {graph.Sum(f => f.Count)} total nodes.");
        ClearMap();

        foreach (var floor in graph)
        {
            foreach (var node in floor)
            {
                // 1. Calculate Base Position
                float spread = (node.percentX - 0.5f) * mapWidthMultiplier;
                float depth = node.floorIndex * floorSpacingY;

                float baseX = 0f;
                float baseY = 0f;

                if (mapGenerator.mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop)
                {
                    baseX = spread;
                    baseY = depth;
                }
                else // LeftToRight
                {
                    baseX = depth;
                    // Multiply by -1 if you want index 0 to start at the top of the screen instead of the bottom
                    baseY = spread * -1f; 
                }

                // 2. Apply Organic Jitter (using the same synced random state)
                float jitterX = UnityEngine.Random.Range(-maxJitter.x, maxJitter.x);
                float jitterY = UnityEngine.Random.Range(-maxJitter.y, maxJitter.y);
                
                // Save the final visual position into the math node so lines can connect perfectly
                node.visualPosition = new Vector2(baseX + jitterX, baseY + jitterY);

                // 3. Spawn the Node GameObject
                GameObject nodeObj = Instantiate(nodePrefab, node.visualPosition, Quaternion.identity, this.transform);
                nodeObj.name = $"Node_{node.floorIndex}_{node.id}";
                spawnedVisuals.Add(nodeObj);

                // 4. Setup Visuals
                SpriteRenderer sr = nodeObj.GetComponent<SpriteRenderer>();
                sr.sortingLayerName = "MapNodes";

                if (node.blueprint.nodeIcon != null)
                {
                    sr.sprite = node.blueprint.nodeIcon;
                }
                else
                {
                    sr.sprite = CreateDebugSprite(node.blueprint.debugColor);
                }

                float maxOriginalSize = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                
                float scaleFactor = targetNodeSize / maxOriginalSize;
                
                nodeObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);

                // Assign the node id and snapshot the normalized scale now that it's final (Awake ran before this scale was applied).
                nodeObj.GetComponent<NodeVisual>().Setup(node, minimumNodeClickRadius);

                // 5. Spawn Decorators
                SpawnDecorators(node, nodeObj.transform);
            }
        }
        if (lineDrawer != null)
        {
            lineDrawer.DrawLines(graph);
        }

        FocusOnCurrentNode();
    }

    private void OnCurrentNodeChanged(int previousNodeId, int currentNodeId)
    {
        FocusOnCurrentNode();
    }

    private void FocusOnCurrentNode()
    {
        if (votingManager == null)
            votingManager = FindFirstObjectByType<MapVotingManager>();
        if (mapCameraController == null)
            mapCameraController = FindFirstObjectByType<MapCameraController>();
        if (votingManager == null || mapCameraController == null)
            return;

        int currentNodeId = votingManager.CurrentNodeId.Value;
        if (currentNodeId < 0 || currentNodeId == focusedNodeId)
            return;

        NodeVisual[] nodes = FindObjectsByType<NodeVisual>(FindObjectsSortMode.None);
        foreach (NodeVisual node in nodes)
        {
            if (node.nodeId != currentNodeId)
                continue;

            focusedNodeId = currentNodeId;
            mapCameraController.FocusOnMapPosition(node.transform.position, returnFocusDuration);
            return;
        }
    }

    private void SpawnDecorators(NodeData node, Transform parentNode)
    {
        if (node.blueprint.decoratorSprites == null || node.blueprint.decoratorSprites.Length == 0) return;

        int count = UnityEngine.Random.Range(node.blueprint.decoratorCountMinMax.x, node.blueprint.decoratorCountMinMax.y + 1);

        for (int i = 0; i < count; i++)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * node.blueprint.decoratorRadius;
            Vector2 decPos = node.visualPosition + randomOffset;

            GameObject decObj = Instantiate(decoratorPrefab, decPos, Quaternion.identity, parentNode);
            spawnedVisuals.Add(decObj);

            SpriteRenderer decSr = decObj.GetComponent<SpriteRenderer>();
            decSr.sortingLayerName = "Decorators";
            
            // Randomly pick a sprite from the blueprint's pool
            Sprite[] pool = node.blueprint.decoratorSprites;
            decSr.sprite = pool[UnityEngine.Random.Range(0, pool.Length)];
        }
    }

    private void ClearMap()
    {
        foreach (var obj in spawnedVisuals)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedVisuals.Clear();
    }

    // Helper method to generate a flat colored square for prototyping
    private Sprite CreateDebugSprite(Color color)
    {
        Texture2D tex = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
    }
}