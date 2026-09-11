using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class MapVotingManager : NetworkBehaviour
{
    [Header("UI & Visuals")]
    public GameObject playerAvatarPrefab; // A tiny UI icon to show who voted

    // Server-only dictionary mapping ClientID to NodeID
    private Dictionary<ulong, int> serverVotes = new Dictionary<ulong, int>();
    private bool isVotingLocked = false;
    public float lotteryDuration = 1.5f;

    // Client-side visual tracking mapping ClientID to Avatar GameObject
    private Dictionary<ulong, GameObject> clientAvatars = new Dictionary<ulong, GameObject>();

    // SERVER LOGIC

    [ServerRpc(RequireOwnership = false)]
    public void SubmitVoteServerRpc(int selectedNodeId, ServerRpcParams rpcParams = default)
    {
        if (isVotingLocked) return; // Ignore if lottery already started

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
        
        // ====================================================================
        // EXTERNAL HOOK 
        // Here is where you load next scene
        // ====================================================================
    }
}