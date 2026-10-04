using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class MapVotingManager : NetworkBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;

    [Header("UI & Visuals")]
    public GameObject playerAvatarPrefab; // A tiny UI icon to show who voted
    [SerializeField] private float voteAvatarGap = 0.08f;
    [SerializeField] private float voteAvatarNodeGap = 0.12f;

    // Server-only dictionary mapping ClientID to NodeID
    private Dictionary<ulong, int> serverVotes = new Dictionary<ulong, int>();
    private bool isVotingLocked = false;
    public float lotteryDuration = 1.5f;

    // Client-side visual tracking mapping ClientID to Avatar GameObject
    private Dictionary<ulong, GameObject> clientAvatars = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, int> clientVoteNodes = new Dictionary<ulong, int>();

    // Synced so clients can gate voting/highlighting to the reachable node set without querying the server.
    public NetworkVariable<int> CurrentNodeId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<int> VisitedNodeIds = new NetworkList<int>();
    public bool IsNodeVisited(int nodeId) => VisitedNodeIds.Contains(nodeId);

    public override void OnNetworkSpawn()
    {
        // Each visit to the map is a fresh vote, even if this object was already spawned before.
        serverVotes.Clear();
        isVotingLocked = false;

        foreach (var avatar in clientAvatars.Values)
        {
            if (avatar != null) Destroy(avatar);
        }
        clientAvatars.Clear();
        clientVoteNodes.Clear();

        if (IsServer)
        {
            VisitedNodeIds.Clear();
            foreach (int id in PersistentGameStateManager.Instance.VisitedMapNodeIds)
                VisitedNodeIds.Add(id);
        }

        if (IsServer && PersistentGameStateManager.Instance != null)
            CurrentNodeId.Value = PersistentGameStateManager.Instance.CurrentMapNodeId;

        PersistentGameStateManager.Instance?.OnMapSceneReady();
    }

    // SERVER LOGIC

    [ServerRpc(RequireOwnership = false)]
    public void SubmitVoteServerRpc(int selectedNodeId, ServerRpcParams rpcParams = default)
    {
        GameLogger.Log(LogSeverity.Debug, $"SubmitVoteServerRpc: selectedNodeId={selectedNodeId}, sender={rpcParams.Receive.SenderClientId}, isVotingLocked={isVotingLocked}, reachable={IsNodeReachable(selectedNodeId)}, CurrentNodeId={CurrentNodeId.Value}");
        if (isVotingLocked) return; // Ignore if lottery already started
        if (!IsNodeReachable(selectedNodeId)) return; // Ignore votes for nodes outside the current path

        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Record the vote
        serverVotes[clientId] = selectedNodeId;

        // Tell EVERYONE to update their visuals so they see this player's avatar move
        UpdateVoteVisualClientRpc(clientId, selectedNodeId);

        // Check if everyone in the lobby has voted
        if (serverVotes.Count >= NetworkManager.Singleton.ConnectedClients.Count)
        {
            isVotingLocked = true;
            Invoke(nameof(ExecuteLottery), lotteryDuration);
        }
    }

    private void ExecuteLottery()
    {
        // Flatten the votes into a list (this natively handles the weight)
        // E.g., if 3 people voted for Node 5, Node 5 is in this list 3 times.
        List<int> ticketsInHat = serverVotes.Values.ToList();

        // Pick a random winner
        int winningIndex = Random.Range(0, ticketsInHat.Count);
        int winningNodeId = ticketsInHat[winningIndex];

        // Announce the winner to all clients
        AnnounceWinnerClientRpc(winningNodeId);

        // ====================================================================
        // EXTERNAL HOOK
        // Route the flow based on which node type was picked.
        // ====================================================================
        NodeData winningNode = mapGenerator != null ? mapGenerator.GetNodeById(winningNodeId) : null;
        if (winningNode != null)
            PersistentGameStateManager.Instance?.OnMapNodeSelected(winningNodeId, winningNode.blueprint.type);
    }

    // Votes are only valid for floor 0 (run start) or nodes reachable from the current map position.
    public bool IsNodeReachable(int nodeId)
    {
        if (mapGenerator == null) return true;

        int currentNodeId = CurrentNodeId.Value;
        if (currentNodeId == -1)
        {
            bool floorZero = mapGenerator.IsFloorZeroNode(nodeId);
            GameLogger.Log(LogSeverity.Debug, $"IsNodeReachable({nodeId}): currentNodeId=-1, IsFloorZeroNode={floorZero}");
            return floorZero;
        }

        NodeData currentNode = mapGenerator.GetNodeById(currentNodeId);
        bool result = currentNode != null && currentNode.connectedNodeIds.Contains(nodeId);
        GameLogger.Log(LogSeverity.Debug, $"IsNodeReachable({nodeId}): currentNodeId={currentNodeId}, currentNode found={currentNode != null}, connections=[{(currentNode == null ? "" : string.Join(",", currentNode.connectedNodeIds))}], result={result}");
        return result;
    }
    // CLIENT LOGIC
    [ClientRpc]
    private void UpdateVoteVisualClientRpc(ulong clientId, int nodeId)
    {
        GameObject targetNode = FindNodeObject(nodeId);
        GameLogger.Log(LogSeverity.Debug, $"UpdateVoteVisualClientRpc: clientId={clientId}, nodeId={nodeId}, targetNode found={targetNode != null}, playerAvatarPrefab null={playerAvatarPrefab == null}");

        if (targetNode != null && playerAvatarPrefab != null)
        {
            if (!clientAvatars.ContainsKey(clientId))
            {
                GameObject newAvatar = Instantiate(playerAvatarPrefab, targetNode.transform.position, Quaternion.identity);
                SpriteRenderer avatarRenderer = newAvatar.GetComponentInChildren<SpriteRenderer>();
                if (avatarRenderer != null)
                {
                    avatarRenderer.sortingLayerName = "PlayerAvatars";
                    ApplyPlayerVoteColor(clientId, avatarRenderer);
                }
                clientAvatars[clientId] = newAvatar;
            }

            bool hadPreviousVote = clientVoteNodes.TryGetValue(clientId, out int previousNodeId);
            clientVoteNodes[clientId] = nodeId;

            if (hadPreviousVote && previousNodeId != nodeId)
            {
                GameObject previousNode = FindNodeObject(previousNodeId);
                if (previousNode != null)
                    RefreshVoteAvatarLayout(previousNodeId, previousNode);
            }

            RefreshVoteAvatarLayout(nodeId, targetNode);
        }
    }

    private GameObject FindNodeObject(int nodeId)
    {
        NodeVisual[] nodes = FindObjectsByType<NodeVisual>(FindObjectsSortMode.None);
        foreach (NodeVisual node in nodes)
        {
            if (node.nodeId == nodeId)
                return node.gameObject;
        }

        return null;
    }

    private async void ApplyPlayerVoteColor(ulong clientId, SpriteRenderer avatarRenderer)
    {
        await Cysharp.Threading.Tasks.UniTask.WaitUntil(() =>
            CursorUIManager.Instance != null && PersistentPlayerRegistry.Instance != null);

        Color playerColor = await CursorUIManager.Instance.GetColorForPlayer(clientId);
        if (avatarRenderer != null)
            avatarRenderer.color = playerColor;
    }

    private void RefreshVoteAvatarLayout(int nodeId, GameObject targetNode)
    {
        var avatars = clientVoteNodes
            .Where(vote => vote.Value == nodeId && clientAvatars.ContainsKey(vote.Key))
            .OrderBy(vote => GetPlayerIndex(vote.Key))
            .ThenBy(vote => vote.Key)
            .Select(vote => clientAvatars[vote.Key])
            .Where(avatar => avatar != null)
            .ToList();

        if (avatars.Count == 0)
            return;

        SpriteRenderer nodeRenderer = targetNode.GetComponent<SpriteRenderer>();
        float nodeBottom = nodeRenderer != null
            ? nodeRenderer.bounds.min.y
            : targetNode.transform.position.y;
        float maxAvatarHeight = 0f;
        float totalWidth = 0f;
        List<float> avatarWidths = new List<float>(avatars.Count);

        foreach (GameObject avatar in avatars)
        {
            SpriteRenderer avatarRenderer = avatar.GetComponentInChildren<SpriteRenderer>();
            Vector3 avatarSize = avatarRenderer != null ? avatarRenderer.bounds.size : Vector3.zero;
            float width = Mathf.Max(avatarSize.x, 0.2f);
            float height = Mathf.Max(avatarSize.y, 0.2f);
            avatarWidths.Add(width);
            totalWidth += width;
            maxAvatarHeight = Mathf.Max(maxAvatarHeight, height);
        }

        totalWidth += voteAvatarGap * (avatars.Count - 1);
        float x = targetNode.transform.position.x - totalWidth * 0.5f;
        float y = nodeBottom - voteAvatarNodeGap - maxAvatarHeight * 0.5f;

        for (int i = 0; i < avatars.Count; i++)
        {
            float width = avatarWidths[i];
            avatars[i].transform.position = new Vector3(
                x + width * 0.5f,
                y,
                targetNode.transform.position.z - 0.1f);
            x += width + voteAvatarGap;
        }
    }

    private static int GetPlayerIndex(ulong clientId)
    {
        PlayerData player = PersistentPlayerRegistry.Instance?.GetByClientId(clientId);
        return player != null ? player.playerIndex : int.MaxValue;
    }

    [ClientRpc]
    private void AnnounceWinnerClientRpc(int winningNodeId)
    {
        Debug.Log($"<color=green>THE LOTTERY HAS FINISHED! Winning Node: {winningNodeId}</color>");
        // Scene routing is handled server-side via PersistentGameStateManager.OnMapNodeSelected.
    }
}