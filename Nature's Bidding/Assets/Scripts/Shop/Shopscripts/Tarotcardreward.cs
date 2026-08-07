using UnityEngine;

[CreateAssetMenu(fileName = "NewTarotCard", menuName = "Bidding/Tarot Card Reward")]
public class TarotCardReward : ScriptableObject
{
    #region Identity

    [Header("Card Identity")]
    public string cardId      = "the_moon";
    public string cardName      = "The Moon";
    [TextArea(2, 4)]
    public string flavorText    = "What is hidden shall be revealed.";
    public Sprite cardFaceSprite;
    public Sprite cardBackSprite;

    #endregion

    #region Reward

    [Header("Reward")]
    public TarotRewardType rewardType;

    [Tooltip("Used for simple stat boosts — percentage as decimal e.g. 0.1 = +10%")]
    public float effectValue = 0f;

    [TextArea(1, 2)]
    public string rewardSummary = "+10% Speed";

    #endregion

    #region Rarity

    [Header("Rarity")]
    [Range(1, 3)]
    public int rarity = 1;

    public Color RarityColor()
    {
        switch (rarity)
        {
            case 2:  return new Color(0.4f, 0.6f, 1f);
            case 3:  return new Color(1f,   0.8f, 0.2f);
            default: return Color.white;
        }
    }

    #endregion
}

/// <summary>
/// All tarot card reward types.
/// Simple stat boosts use PlayerData NetworkVariables directly.
/// Complex effects use PlayerEffects NetworkVariables.
/// </summary>
public enum TarotRewardType
{
    // ── Simple stat boosts (effectValue = multiplier delta) ──────────────────
    Chariot,            // +Speed
    Magician,           // +Jump
    Empress,            // +Attack speed
    HighPriestess,      // +Health
    Star,               // +All stats
    Tower,              // +Health, -Damage
    Hermit,             // +Damage, -Health

    // ── Conditional / triggered effects ──────────────────────────────────────
    Emperor,            // Double damage if you have the most coins
    World,              // More damage at lower health (scales)
    Fool,               // Super fast but can only attack every 10s
    Hanged,             // Your fastest opponent is slowed

    // ── Persistent aura effects ───────────────────────────────────────────────
    Lovers,             // Two random opponents share health pools
    Justice,            // Thorns — attackers take damage
    Sun,                // Bright glow + AoE damage on attack
    Moon,               // Glide across ground (reduced friction)
    Devil,              // Lifesteal on hit

    // ── Meta ─────────────────────────────────────────────────────────────────
    WheelOfFortune,     // Apply two random other tarot effects
    Coins,              // Legacy coin reward
    Reroll,             // Free reroll in shop
}