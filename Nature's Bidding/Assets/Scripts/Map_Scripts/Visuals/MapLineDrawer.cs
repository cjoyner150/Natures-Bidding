using System.Collections.Generic;
using UnityEngine;

public class MapLineDrawer : MonoBehaviour
{
    public MapSettingsSO mapSettings;
    public GameObject linePrefab;
    public float curveSteepness = 0.5f;
    public int resolution = 20;

    private List<GameObject> drawnLines = new List<GameObject>();

    // MADE THIS PUBLIC. REMOVED START() AND ONDESTROY()
    public void DrawLines(List<List<NodeData>> graph) 
    {
        ClearLines();

        Dictionary<int, NodeData> nodeLookup = new Dictionary<int, NodeData>();
        foreach (var floor in graph)
        {
            foreach (var node in floor) nodeLookup[node.id] = node;
        }

        foreach (var floor in graph)
        {
            foreach (var node in floor)
            {
                foreach (int targetId in node.connectedNodeIds)
                {
                    if (nodeLookup.TryGetValue(targetId, out NodeData targetNode))
                    {
                        DrawBezierCurve(node.visualPosition, targetNode.visualPosition);
                    }
                }
            }
        }
    }

    private void DrawBezierCurve(Vector2 start, Vector2 end)
    {
        GameObject lineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity, this.transform);
        drawnLines.Add(lineObj);

        LineRenderer lr = lineObj.GetComponent<LineRenderer>();
        lr.positionCount = resolution;
        lr.useWorldSpace = true;
        lr.sortingLayerName = "MapLines"; 

        Vector2 control1 = Vector2.zero;
        Vector2 control2 = Vector2.zero;

        if (mapSettings.orientation == MapSettingsSO.MapOrientation.BottomToTop)
        {
            control1 = start + (Vector2.up * curveSteepness);
            control2 = end + (Vector2.down * curveSteepness);
        }
        else // LeftToRight
        {
            control1 = start + (Vector2.right * curveSteepness);
            control2 = end + (Vector2.left * curveSteepness);
        }

        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            Vector2 pixel = CalculateCubicBezierPoint(t, start, control1, control2, end);
            lr.SetPosition(i, new Vector3(pixel.x, pixel.y, 0f));
        }
    }

    private Vector2 CalculateCubicBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float u = 1 - t; float tt = t * t; float uu = u * u;
        float uuu = uu * u; float ttt = tt * t;

        Vector2 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;

        return p;
    }

    private void ClearLines()
    {
        foreach (var line in drawnLines) if (line != null) Destroy(line);
        drawnLines.Clear();
    }
}