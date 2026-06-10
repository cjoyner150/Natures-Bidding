using System.Collections.Generic;

public class PersistentPlayerData
{
    public ulong clientId;
    public string authenticationId; // authenticationId is more stable identifier for reconnection
    public string playerName;
    public int playerIndex; // 0-3, controls color and UI position
    public int gold;
    public int combatWins;

    // stringIds for items are internal names that are mapped to scriptable objs
    public List<string> masks = new();
    public List<string> tarotCards = new();
    public List<string> artifacts = new();
}