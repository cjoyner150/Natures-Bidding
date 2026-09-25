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

    // Server-only dictionary mapping ClientID to NodeID
    private Dictionary<ulong, int> serverVotes = new Dictionary<ulong, int>();
    private bool isVotingLocked = false;
    public float lotteryDuration = 1.5f;

    // Client-side visual tracking mapping ClientID to Avatar GameObject
    private Dictionary<ulong, GameObject> clientAvatars = new Dictionary<ulong, GameObject>();

    // Synced so clients can gate voting/highlighting to the reachable node set without querying the server.
    public NetworkVariable<int> CurrentNodeId = new NetworkVariable<int>(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        // Find the physical node object in the scene
        GameObject targetNode = GameObject.Find($"Node_*_{nodeId}"); // Uses the name we set in MapRenderer
        if (targetNode == null)
        {
            // Fallback search if wildcard fails
            NodeVisual[] nodes = FindObjectsByType<NodeVisual>(FindObjectsSortMode.None);
            foreach (var n in nodes) { if (n.nodeId == nodeId) targetNode = n.gameObject; }
        }
        GameLogger.Log(LogSeverity.Debug, $"UpdateVoteVisualClientRpc: clientId={clientId}, nodeId={nodeId}, targetNode found={targetNode != null}, playerAvatarPrefab null={playerAvatarPrefab == null}");

        if (targetNode != null)
        {
            // If this player hasn't voted yet, create their avatar
            if (!clientAvatars.ContainsKey(clientId))
            {
                GameObject newAvatar = Instantiate(playerAvatarPrefab, targetNode.transform.position, Quaternion.identity);
                newAvatar.GetComponent<SpriteRenderer>().sortingLayerName = "PlayerAvatars";
                clientAvatars[clientId] = newAvatar;
            }

            // Move their avatar to the new node (You can replace this with a smooth Vector3.Lerp later)
            clientAvatars[clientId].transform.position = targetNode.transform.position + new Vector3(0.5f, 0.5f, 0); 
        }
    }

    [ClientRpc]
    private void AnnounceWinnerClientRpc(int winningNodeId)
    {
        Debug.Log($"<color=green>THE LOTTERY HAS FINISHED! Winning Node: {winningNodeId}</color>");
        // Scene routing is handled server-side via PersistentGameStateManager.OnMapNodeSelected.
    }
}