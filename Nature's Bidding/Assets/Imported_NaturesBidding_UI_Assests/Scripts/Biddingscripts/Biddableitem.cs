using UnityEngine;

/// <summary>
/// BiddableItem — ScriptableObject representing one auctionable item (the "mask").
/// Create via: Right-click in Project → Create → Bidding → Biddable Item
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Bidding/Biddable Item")]
public class BiddableItem : ScriptableObject
{
    [Header("Identity")]
    public string itemName        = "Mystery Mask";
    [TextArea(2, 5)]
    public string description     = "A strange artifact of unknown origin.";
    public Sprite icon;                  // Used in 2D UI panels

    [Header("3D Display")]
    public GameObject displayPrefab;     // Spawned in the arena when this item is up for bid
    public Vector3 displayRotation;      // Optional starting rotation for the spawned prefab
    public float displayScale = 1f;

    [Header("Auction")]
    public int startingBid = 10;         // Minimum valid bid
    public int rarity = 1;               // 1 = Common, 2 = Rare, 3 = Legendary (for UI colour)

    // Convenience: rarity colour for UI
    public Color RarityColor()
    {
        switch (rarity)
        {
            case 2:  return new Color(0.4f, 0.6f, 1f);   // Blue — Rare
            case 3:  return new Color(1f, 0.8f, 0.2f);   // Gold — Legendary
            default: return Color.white;                   // Common
        }
    }
}