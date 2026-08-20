using UnityEngine;

public class NodeVisual : MonoBehaviour
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

    private void OnMouseEnter()
    {
        spriteRenderer.color = Color.yellow; // Highlight color
        transform.localScale = originalScale * 1.1f;
    }

    private void OnMouseExit()
    {
        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
    }

    private void OnMouseDown()
    {
        // Find the Network Voting Manager
        MapVotingManager votingManager = FindFirstObjectByType<MapVotingManager>();
        
        if (votingManager != null)
        {
            // Tell the server we want to vote for this node
            votingManager.SubmitVoteServerRpc(nodeId);
        }
    }

    // A helper method called by MapRenderer to link this visual to the math data
    public void Setup(NodeData data)
    {
        nodeId = data.id;
    }
}