using UnityEngine;

/// <summary>
/// ShopUpgrade — ScriptableObject for a purchasable upgrade shown in the shop phase.
/// Create via: Right-click in Project → Create → Bidding → Shop Upgrade
/// </summary>
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Bidding/Shop Upgrade")]
public class ShopUpgrade : ScriptableObject
{
    [Header("Identity")]
    public string upgradeName    = "Speed Boost";
    [TextArea(2, 4)]
    public string description    = "Increases movement speed by 5%.";
    public Sprite icon;

    [Header("Effect")]
    public UpgradeType upgradeType;
    public float effectValue     = 0.05f;   // e.g. 0.05 = 5%

    [Header("Shop")]
    public int cost              = 50;
    public int maxPurchases      = 3;        // How many times one player can buy this
    public bool isConsumable     = false;    // True = single use item, false = persistent stat boost

    [Header("Visuals")]
    public Color cardColor       = new Color(0.15f, 0.15f, 0.2f, 1f);

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