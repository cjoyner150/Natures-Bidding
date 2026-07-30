using UnityEngine;

/// <summary>
/// PotType — ScriptableObject defining a pot's behaviour and visuals.
/// Create via: Right-click → Create → Bidding → Pot Type
///
/// Example setups:
///   Small Pot: cost=20, clicksToOpen=3, cardsToDraw=3, cardsToKeep=1
///   Grand Pot: cost=50, clicksToOpen=5, cardsToDraw=5, cardsToKeep=2
/// </summary>
[CreateAssetMenu(fileName = "NewPotType", menuName = "Bidding/Pot Type")]
public class PotType : ScriptableObject
{
    #region Identity

    [Header("Identity")]
    public string potName        = "Pot of Fate";
    public string description    = "Draw 3 tarot cards, choose 1 reward.";
    public int    cost           = 30;

    #endregion

    #region Behaviour

    [Header("Behaviour")]
    [Tooltip("How many times the player must click the pot before it explodes.")]
    public int clicksToOpen  = 3;

    [Tooltip("How many tarot cards are drawn and shown face-down.")]
    public int cardsToDraw   = 3;

    [Tooltip("How many cards the player may select and keep.")]
    public int cardsToKeep   = 1;

    #endregion

    #region Visuals

    [Header("Visuals")]
    [Tooltip("Sprites cycled through as the player clicks — last sprite is the 'about to explode' state.")]
    public Sprite[] clickSprites;

    [Tooltip("Shown during the explosion flash before cards appear.")]
    public Sprite   explodeSprite;

    [Tooltip("Particle prefab or GameObject spawned at explosion moment (optional).")]
    public GameObject explodeEffect;

    #endregion
}