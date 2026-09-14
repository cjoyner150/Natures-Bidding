using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

#pragma warning disable IDE0051

[Preserve]
public static class GameplayCommands
{
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

    [ConsoleCommand("timescale", "Sets Time.timeScale")]
    static void SetTimeScale(float scale = 1f) => Time.timeScale = scale;

    [ConsoleCommand("loglevel", "Sets console log verbosity")]
    static string SetLogLevel(LogSeverity level)
    {
        GameLogger.CurrentLevel = level;
        return $"Log level set to {level}";
    }

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


    [ConsoleCommand("help", "Lists all commands")]
    static string Help() =>
        string.Join("\n", CommandRegistry.All.OrderBy(c => c.Name).Select(c => $"{c.Usage}  — {c.Description}"));
}