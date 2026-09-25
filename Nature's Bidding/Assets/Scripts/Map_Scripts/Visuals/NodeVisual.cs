using UnityEngine;
using UnityEngine.EventSystems;

public class NodeVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    // The mathematical ID of this node, assigned by the MapRenderer
    public int nodeId; 

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale; 

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        originalScale = transform.localScale;
    }

    // Uses EventSystem pointer events (not legacy OnMouseX) so clicks register correctly with the Input System package.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsReachable()) return;

        spriteRenderer.color = Color.yellow; // Highlight color
        transform.localScale = originalScale * 1.1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        bool reachable = IsReachable();
        GameLogger.Log(LogSeverity.Debug, $"NodeVisual.OnPointerDown: nodeId={nodeId}, reachable={reachable}");
        if (!reachable) return;

        // Find the Network Voting Manager
        MapVotingManager votingManager = FindFirstObjectByType<MapVotingManager>();
        
        if (votingManager != null)
        {
            // Tell the server we want to vote for this node
            votingManager.SubmitVoteServerRpc(nodeId);
        }
    }

    private bool IsReachable()
    {
        MapVotingManager votingManager = FindFirstObjectByType<MapVotingManager>();
        return votingManager == null || votingManager.IsNodeReachable(nodeId);
    }

    // A helper method called by MapRenderer to link this visual to the math data, once its final display scale is set.
    public void Setup(NodeData data)
    {
        nodeId = data.id;
        originalScale = transform.localScale;
    }
}