using MoreMountains.Feedbacks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class NodeVisual : MonoBehaviour
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

    [Header("Visited")]
    [SerializeField] private GameObject visitedMarker;

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
        {
            votingManager.CurrentNodeId.OnValueChanged += OnCurrentNodeChanged;
            votingManager.VisitedNodeIds.OnListChanged += OnVisitedChanged;
        }

        RefreshReachabilityVisual();
        RefreshVisitedVisual();
    }

    private void OnDestroy()
    {
        if (votingManager != null)
        {
            votingManager.CurrentNodeId.OnValueChanged -= OnCurrentNodeChanged;
            votingManager.VisitedNodeIds.OnListChanged -= OnVisitedChanged;
        }
    }

    private void OnVisitedChanged(NetworkListEvent<int> _) => RefreshVisitedVisual();

    private void RefreshVisitedVisual()
    {
        if (visitedMarker == null) return;
        bool visited = votingManager != null && votingManager.IsNodeVisited(nodeId);
        visitedMarker.SetActive(visited);
    }

    public void HoverEnter()
    {
        if (!IsReachable()) return;

        spriteRenderer.color = Color.yellow;
        if (hoverFeedback != null) hoverFeedback.PlayFeedbacks();
        else transform.localScale = originalScale * 1.1f;
    }

    public void HoverExit()
    {
        if (hoverFeedback != null) hoverFeedback.PlayFeedbacksInReverse();
        else transform.localScale = originalScale;
        RefreshReachabilityVisual();
    }

    public void Select()
    {
        bool reachable = IsReachable();
        GameLogger.Log(LogSeverity.Debug, $"NodeVisual.Select: nodeId={nodeId}, reachable={reachable}");
        if (!reachable) return;

        clickFeedback?.PlayFeedbacks();
        votingManager ??= FindFirstObjectByType<MapVotingManager>();
        votingManager?.SubmitVoteServerRpc(nodeId);
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