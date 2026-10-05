using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

#pragma warning disable IDE0051

[Preserve]
public static class GameplayCommands
{

    #region give_artifact overloads

    [ConsoleCommand("give_artifact", "Gives an artifact to a client by client ID or auth ID")]
    public static string GiveArtifact(string target, string artifact)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = ulong.TryParse(target, out ulong clientId)
            ? registry.GetByClientId(clientId)
            : registry.GetByAuthId(target);

        if (player == null) return $"No player found for '{target}'";

        registry.AddItem(player.clientId, artifact, ItemType.Artifact);
        return $"Gave {artifact} to {player.playerName} (client {player.clientId})";
    }

    [ConsoleCommand("give_artifact", "Gives an artifact to the local client")]
    public static string GiveArtifact(string artifact)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry.GetByClientId(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        if (player == null) return "No local player found";

        registry.AddItem(player.clientId, artifact, ItemType.Artifact);
        return $"Gave {artifact} to {player.playerName} (client {player.clientId})";
    }
    #endregion

    #region give_mask overloads

    [ConsoleCommand("give_mask", "Gives a mask to a client by client ID or auth ID")]
    public static string GiveMask(string target, string mask)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = ulong.TryParse(target, out ulong clientId)
            ? registry.GetByClientId(clientId)
            : registry.GetByAuthId(target);

        if (player == null) return $"No player found for '{target}'";

        registry.AddItem(player.clientId, mask, ItemType.Mask);
        return $"Gave {mask} to {player.playerName} (client {player.clientId})";
    }

    [ConsoleCommand("give_mask", "Gives a mask to the local client")]
    public static string GiveMask(string mask)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry.GetByClientId(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        if (player == null) return "No local player found";

        registry.AddItem(player.clientId, mask, ItemType.Mask);
        return $"Gave {mask} to {player.playerName} (client {player.clientId})";
    }
    #endregion

    #region give_tarot overloads

    [ConsoleCommand("give_tarot", "Gives a tarot card to a client by client ID or auth ID")]
    public static string GiveTarot(string target, string tarot)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = ulong.TryParse(target, out ulong clientId)
            ? registry.GetByClientId(clientId)
            : registry.GetByAuthId(target);

        if (player == null) return $"No player found for '{target}'";

        registry.AddItem(player.clientId, tarot, ItemType.TarotCard);
        return $"Gave {tarot} to {player.playerName} (client {player.clientId})";
    }

    [ConsoleCommand("give_tarot", "Gives a tarot card to the local client")]
    public static string GiveTarot(string tarot)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry.GetByClientId(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        if (player == null) return "No local player found";

        registry.AddItem(player.clientId, tarot, ItemType.TarotCard);
        return $"Gave {tarot} to {player.playerName} (client {player.clientId})";
    }
    #endregion

    #region give_gold overloads

    [ConsoleCommand("give_gold", "Gives gold to a client by client ID or auth ID")]
    public static string GiveGold(string target, int amount)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = ulong.TryParse(target, out ulong clientId)
            ? registry.GetByClientId(clientId)
            : registry.GetByAuthId(target);

        if (player == null) return $"No player found for '{target}'";

        registry.AddGold(player.clientId, amount);
        return $"Gave {amount} gold to {player.playerName} (client {player.clientId})";
    }

    [ConsoleCommand("give_gold", "Gives gold to the local client")]
    public static string GiveGold(int amount)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry.GetByClientId(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        if (player == null) return "No local player found";

        registry.AddGold(player.clientId, amount);
        return $"Gave {amount} gold to {player.playerName} (client {player.clientId})";
    }
    #endregion

    #region show_player_data overloads

    [ConsoleCommand("show_player_data", "Displays data for a specific player by client ID or auth ID")]
    static string ShowPlayerData(string target)
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = ulong.TryParse(target, out ulong clientId)
            ? registry.GetByClientId(clientId)
            : registry.GetByAuthId(target);

        if (player == null) return $"No player found for '{target}'";

        return $"Player Data for {player.playerName}:\nClient ID: {player.clientId}\nAuth ID: {player.authenticationId}\nGold: {player.gold}" +
            $"\nArtifacts: {string.Join(", ", player.artifacts)}\nTarot Cards: {string.Join(", ", player.tarotCards)}\nMasks: {string.Join(", ", player.masks)}";
    }

    [ConsoleCommand("show_player_data", "Displays data for the local player")]
    static string ShowPlayerData()
    {
        var registry = PersistentPlayerRegistry.Instance;
        var player = registry.GetByClientId(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);

        if (player == null) return "No local player found";

        return $"Player Data for {player.playerName}:\nClient ID: {player.clientId}\nAuth ID: {player.authenticationId}\nGold: {player.gold}" +
            $"\nArtifacts: {string.Join(", ", player.artifacts)}\nTarot Cards: {string.Join(", ", player.tarotCards)}\nMasks: {string.Join(", ", player.masks)}";
    }
    #endregion

    #region scene transition commands
    [ConsoleCommand("scene", "loads a scene based on the node type")]
    public static string LoadScene(NodeType nodeType)
    {
        if (PersistentGameStateManager.Instance == null) return "Game state manager not available";

        switch (nodeType)
        {
            case NodeType.Bidding:
                PersistentGameStateManager.Instance.LoadBiddingLevel();
                break;
            case NodeType.Shop:
                PersistentGameStateManager.Instance.LoadShopLevel();
                break;
            case NodeType.Tarot:
                PersistentGameStateManager.Instance.LoadTarotLevel();
                break;
            case NodeType.Fight:
                PersistentGameStateManager.Instance.BeginCombatPhaseServer();
                break;
            default:
                PersistentGameStateManager.Instance.LoadMapLevel();
                return $"No scene transition defined for node type: {nodeType}";
        }

        return $"Loading scene for node type: {nodeType}";
    }
    #endregion

    #region map debug commands

    [ConsoleCommand("select_last_node", "Resolves a final-floor node immediately, skipping the rest of the map (server only)")]
    public static string SelectLastNode(int choice = 0)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return "Only the host can select map nodes.";

        var generator = Object.FindFirstObjectByType<MapGenerator>();
        if (generator == null || !generator.HasGeneratedData) return "No generated map in this scene.";

        var finalFloor = generator.CurrentGraph[generator.CurrentGraph.Count - 1];
        if (finalFloor.Count == 0) return "Final floor has no nodes.";

        choice = Mathf.Clamp(choice, 0, finalFloor.Count - 1);
        var node = finalFloor[choice];

        PersistentGameStateManager.Instance.OnMapNodeSelected(node.id, node.blueprint.type);
        return $"Selected final-floor node {node.id} ({node.blueprint.name}). Completing it will trigger map regeneration.";
    }

    [ConsoleCommand("goto_floor", "Moves the party's current map position to a node on the given floor so the next vote starts from there (server only)")]
    public static string GotoFloor(int floorIndex, int choice = 0)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return "Only the host can move the party.";

        var generator = Object.FindFirstObjectByType<MapGenerator>();
        var voting = Object.FindFirstObjectByType<MapVotingManager>();
        if (generator == null || !generator.HasGeneratedData) return "No generated map in this scene.";
        if (voting == null) return "No MapVotingManager in this scene.";

        var graph = generator.CurrentGraph;
        if (floorIndex < 0 || floorIndex >= graph.Count) return $"Floor must be 0-{graph.Count - 1}.";

        var floor = graph[floorIndex];
        choice = Mathf.Clamp(choice, 0, floor.Count - 1);
        var node = floor[choice];

        voting.CurrentNodeId.Value = node.id;   // server-writable NetworkVariable; camera refocus + vote options follow
        return $"Party moved to floor {floorIndex}, node {node.id} ({node.blueprint.name}). Vote from here.";
    }

    #endregion

    #region utility commands
    [ConsoleCommand("timescale", "Sets Time.timeScale")]
    static void SetTimeScale(float scale = 1f) => Time.timeScale = scale;

    [ConsoleCommand("loglevel", "Sets console log verbosity")]
    static string SetLogLevel(LogSeverity level)
    {
        GameLogger.CurrentLevel = level;
        return $"Log level set to {level}";
    }
    [ConsoleCommand("help", "Lists all commands")]
    static string Help() =>
        string.Join("\n", CommandRegistry.All.OrderBy(c => c.Name).Select(c => $"{c.Usage}  — {c.Description}"));

    #endregion
}