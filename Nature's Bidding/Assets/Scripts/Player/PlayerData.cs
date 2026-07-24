using System.Collections.Generic;

public class PlayerData
{
    public ulong clientId;
    public string authenticationId; // authenticationId is more stable identifier for reconnection
    public string playerName;
    public int playerIndex; // 0-3, controls color and UI position
    public int gold;
    public int combatWins;

    // UI faux frontend info
    public float speedMultiplier = 1f;
    public float jumpMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float defenseMultiplier = 1f;
    public float maxHealthBonus = 0f;
    public List<string> items = new();
    public Dictionary<string, int> upgradeCounts = new();

    // stringIds for items are internal names that are mapped to scriptable objs
    public List<string> masks = new();
    public List<string> tarotCards = new();
    public List<string> artifacts = new();

    public List<StatusEffectorSO> GetMaskEffectors() =>
        GameDataManager.Instance.GetEffectors(masks);

    public List<StatusEffectorSO> GetTarotEffectors() =>
        GameDataManager.Instance.GetEffectors(tarotCards);

    public List<StatusEffectorSO> GetArtifactEffectors() =>
        GameDataManager.Instance.GetEffectors(artifacts);
}