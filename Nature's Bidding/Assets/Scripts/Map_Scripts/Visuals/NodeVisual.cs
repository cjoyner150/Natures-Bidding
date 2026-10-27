using UnityEngine;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;

public class NodeVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    // The mathematical ID of this node, assigned by the MapRenderer
    public int nodeId; 

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale;
    private MapVotingManager votingManager;

    [Header("Feel Feedbacks")]
    [SerializeField] private MMF_Player hoverFeedback;
    [SerializeField] private MMF_Player clickFeedback;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        originalScale = transform.localScale;
    }

    private void Start()
    {
        votingManager = FindFirstObjectByType<MapVotingManager>();
        if (votingManager != null)
            votingManager.CurrentNodeId.OnValueChanged += OnCurrentNodeChanged;

        RefreshReachabilityVisual();
    }

    private void OnDestroy()
    {
        if (votingManager != null)
            votingManager.CurrentNodeId.OnValueChanged -= OnCurrentNodeChanged;
    }

    // Uses EventSystem pointer events (not legacy OnMouseX) so clicks register correctly with the Input System package.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsReachable()) return;

        spriteRenderer.color = Color.yellow; // Highlight color
        if (hoverFeedback != null)
            hoverFeedback.PlayFeedbacks();
        else
            transform.localScale = originalScale * 1.1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverFeedback != null)
            hoverFeedback.PlayFeedbacksInReverse();
        else
            transform.localScale = originalScale;
        RefreshReachabilityVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        bool reachable = IsReachable();
        GameLogger.Log(LogSeverity.Debug, $"NodeVisual.OnPointerDown: nodeId={nodeId}, reachable={reachable}");
        if (!reachable) return;

        clickFeedback?.PlayFeedbacks();

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
        if (votingManager == null)
            votingManager = FindFirstObjectByType<MapVotingManager>();

        return votingManager == null || votingManager.IsNodeReachable(nodeId);
    }

    private void OnCurrentNodeChanged(int previousNodeId, int currentNodeId)
    {
        RefreshReachabilityVisual();
    }

    private void RefreshReachabilityVisual()
    {
        spriteRenderer.color = IsReachable() ? originalColor : Color.gray;
    }

    // A helper method called by MapRenderer to link this visual to the math data, once its final display scale is set.
    public void Setup(NodeData data, float minimumClickRadius)
    {
        nodeId = data.id;
        originalScale = transform.localScale;

        CircleCollider2D clickCollider = GetComponent<CircleCollider2D>();
        if (clickCollider != null)
        {
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);
            clickCollider.radius = Mathf.Max(clickCollider.radius, minimumClickRadius / scale);
        }
    }
}