using UnityEngine;

[CreateAssetMenu(fileName = "NewNodeBlueprint", menuName = "Map/Node Blueprint")]
public class NodeBlueprintSO : ScriptableObject
{
    public string nodeName;
    public NodeType type;
    
    [Header("Generation")]
    [Tooltip("Higher numbers mean this node is more likely to be picked during random generation.")]
    [Range(0f, 100f)] 
    public float spawnWeight = 10f; 

    [Header("Visuals (2D World Space)")]
    public Sprite nodeIcon;
    [Tooltip("Used by the debug renderer if no sprite is assigned.")]
    public Color debugColor = Color.white; 
    
    [Header("Environment Decorators")]
    public Sprite[] decoratorSprites;
    public Vector2Int decoratorCountMinMax = new Vector2Int(0, 3);
    public float decoratorRadius = 1.5f;
}