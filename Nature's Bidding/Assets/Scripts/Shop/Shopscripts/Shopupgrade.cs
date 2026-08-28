using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ShopUpgrade — ScriptableObject for a purchasable upgrade shown in the shop phase.
/// Create via: Right-click in Project → Create → Bidding → Shop Upgrade
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Bidding/Shop Upgrade")]
public class ShopUpgrade : ScriptableObject
{
    private static readonly Dictionary<string, ShopUpgrade> _registry = new Dictionary<string, ShopUpgrade>();

    [Header("Identity")]
    [SerializeField, HideInInspector] private string _id;
    public string upgradeName    = "Speed Boost";
    [TextArea(2, 4)]
    public string description    = "Increases movement speed by 5%.";
    public Sprite icon;

    [Header("Effectors")]
    public string effectorId = string.Empty;
    public ItemType effectorBucket = ItemType.Artifact;

    [Header("Effect")]
    public UpgradeType upgradeType;
    public float effectValue     = 0.05f;   // e.g. 0.05 = 5%

    [Header("Shop")]
    public int cost              = 50;
    public int maxPurchases      = 3;        // How many times one player can buy this
    public bool isConsumable     = false;    // True = single use item, false = persistent stat boost

    [Header("Visuals")]
    public Color cardColor       = new Color(0.15f, 0.15f, 0.2f, 1f);
    public Sprite shadowSprite;

    public string Id => _id;

    public static bool TryGet(string id, out ShopUpgrade upgrade)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            upgrade = null;
            return false;
        }

        return _registry.TryGetValue(id, out upgrade);
    }

    private void OnEnable()
    {
        UpdateIdentity();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateIdentity();
    }
#endif

    private void UpdateIdentity()
    {
        string assetName = name;
        if (string.IsNullOrWhiteSpace(assetName))
            return;

        string normalizedId = assetName.ToLower().Replace(" ", "_");
        bool changed = _id != normalizedId;

        if (changed)
            _id = normalizedId;

        if (_registry.TryGetValue(_id, out ShopUpgrade existing) && existing != null && existing != this)
        {
            GameLogger.Log(LogSeverity.Warning, $"[ShopUpgrade] Duplicate ID '{_id}' found on '{name}'. Existing asset: '{existing.name}'.");
            return;
        }

        _registry[_id] = this;
    }

    public string FormattedEffect()
    {
        switch (upgradeType)
        {
            case UpgradeType.SpeedPercent:    return $"+{effectValue * 100f:0}% Speed";
            case UpgradeType.JumpPercent:     return $"+{effectValue * 100f:0}% Jump Height";
            case UpgradeType.DamagePercent:   return $"+{effectValue * 100f:0}% Damage";
            case UpgradeType.DefensePercent:  return $"+{effectValue * 100f:0}% Defence";
            case UpgradeType.CoinBonus:       return $"+{(int)effectValue} Coins";
            case UpgradeType.HealthPercent:   return $"+{effectValue * 100f:0}% Max Health";
            default:                           return description;
        }
    }

}

public enum UpgradeType
{
    SpeedPercent,
    JumpPercent,
    DamagePercent,
    DefensePercent,
    CoinBonus,
    HealthPercent,
    Custom          // Hook into your own game logic
}