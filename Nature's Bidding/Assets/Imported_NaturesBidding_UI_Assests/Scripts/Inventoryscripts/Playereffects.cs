using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// PlayerEffects — Tracks all complex tarot card effects on a player.
/// Add this to the PlayerData prefab alongside PlayerData.cs.
///
/// Simple stat boosts (Speed, Jump, Damage, Health) live on PlayerData NetworkVariables.
/// This component handles everything that can't be expressed as a single multiplier.
///
/// Your character controller should read these flags each frame / on hit and apply
/// the described behaviour. The descriptions below tell you exactly what to implement.
/// </summary>
public class PlayerEffects : NetworkBehaviour
{
    #region Static Registry

    private static Dictionary<ulong, PlayerEffects> _registry = new Dictionary<ulong, PlayerEffects>();

    public static PlayerEffects GetEffects(ulong clientId)
    {
        _registry.TryGetValue(clientId, out var e);
        return e;
    }

    public static IEnumerable<PlayerEffects> GetAll() => _registry.Values;

    #endregion

    #region Lifecycle

    public override void OnNetworkSpawn()
    {
        _registry[OwnerClientId] = this;
    }

    public override void OnNetworkDespawn()
    {
        _registry.Remove(OwnerClientId);
    }

    #endregion

    #region Conditional Damage Effects

    /// <summary>
    /// THE EMPEROR — True if this player currently has the most coins.
    /// When true: double all outgoing damage.
    /// Evaluated server-side each time damage is dealt.
    /// </summary>
    public NetworkVariable<bool> EmperorActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// THE WORLD — Damage multiplier scales with missing health.
    /// Formula: bonusDamage = (1 - currentHealth/maxHealth) * worldDamageBonus
    /// WorldDamageBonus is how much extra damage at 0hp (e.g. 1.0 = +100% at 0hp).
    /// </summary>
    public NetworkVariable<float> WorldDamageBonus = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Aura / On-Hit Effects

    /// <summary>
    /// JUSTICE (THORNS) — When an attacker deals damage to this player,
    /// reflect this percentage of the damage back to them.
    /// e.g. 0.25 = 25% of incoming damage reflected.
    /// </summary>
    public NetworkVariable<float> ThornsDamagePercent = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// THE DEVIL (LIFESTEAL) — When this player deals damage,
    /// heal for this percentage of the damage dealt.
    /// e.g. 0.15 = heal 15% of damage dealt.
    /// </summary>
    public NetworkVariable<float> LifestealPercent = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// THE SUN — Player emits a bright light and deals AoE damage when attacking.
    /// When true: apply SunAoeDamage to all enemies within SunAoeRadius on each attack.
    /// Also: set your character's light intensity to something ridiculous.
    /// </summary>
    public NetworkVariable<bool>  SunActive    = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SunAoeDamage = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SunAoeRadius = new NetworkVariable<float>(
        5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Movement Effects

    /// <summary>
    /// THE MOON (GLIDE) — Player slides across the ground.
    /// When true: set ground friction/drag to MoonFriction in your character controller.
    /// Normal friction is 1.0 — lower = more slide.
    /// </summary>
    public NetworkVariable<bool>  MoonActive  = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> MoonFriction = new NetworkVariable<float>(
        0.05f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// THE FOOL — Super fast but can only attack once every 10 seconds.
    /// When true: multiply movement speed by FoolSpeedMult, and enforce FoolAttackCooldown.
    /// </summary>
    public NetworkVariable<bool>  FoolActive       = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> FoolSpeedMult    = new NetworkVariable<float>(
        2.5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> FoolAttackCooldown = new NetworkVariable<float>(
        10f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Opponent-Targeting Effects

    /// <summary>
    /// THE HANGED MAN — The player's fastest opponent gets a speed penalty.
    /// Evaluated server-side whenever a speed check is needed.
    /// HangedSpeedPenalty is how much to subtract from the target's SpeedMultiplier.
    /// e.g. 0.3 = target loses 30% speed.
    /// The target is re-evaluated each round (fastest opponent may change).
    /// </summary>
    public NetworkVariable<bool>  HangedActive       = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> HangedSpeedPenalty = new NetworkVariable<float>(
        0.3f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// THE LOVERS — Two random opponents share a health pool.
    /// LoversPartnerA and LoversPartnerB are the clientIds of the linked pair.
    /// When one takes damage, apply the same damage to the other.
    /// Set by server when the card is activated. ulong.MaxValue = not linked.
    /// </summary>
    public NetworkVariable<ulong> LoversPartnerA = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> LoversPartnerB = new NetworkVariable<ulong>(
        ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Attack Speed

    /// <summary>
    /// THE EMPRESS — Increases attack speed.
    /// Multiply your attack animation speed and cooldown by this value.
    /// e.g. 1.3 = 30% faster attacks.
    /// </summary>
    public NetworkVariable<float> AttackSpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Server Helpers

    /// <summary>Called server-side to re-evaluate Emperor status for all players.</summary>
    public static void ServerUpdateEmperorStatus()
    {
        // Find player with most coins
        ulong richestId = ulong.MaxValue;
        int   maxCoins  = -1;
        bool  tie       = false;

        foreach (var pd in PlayerData.GetAllPlayers())
        {
            if (pd.Coins.Value > maxCoins)
            {
                maxCoins  = pd.Coins.Value;
                richestId = pd.OwnerClientId;
                tie       = false;
            }
            else if (pd.Coins.Value == maxCoins)
            {
                tie = true;
            }
        }

        // Update all players
        foreach (var kvp in _registry)
        {
            var fx = kvp.Value;
            if (fx.EmperorActive.Value) // Only update players who have the Emperor card
                fx.EmperorActive.Value = !tie && kvp.Key == richestId;
        }
    }

    /// <summary>Called server-side to find and apply Hanged Man slow to the fastest opponent.</summary>
    public void ServerApplyHangedMan(ulong ownerClientId)
    {
        if (!IsServer || !HangedActive.Value) return;

        // Find fastest opponent (highest SpeedMultiplier excluding self)
        ulong fastestId      = ulong.MaxValue;
        float fastestSpeed   = -1f;

        foreach (var pd in PlayerData.GetAllPlayers())
        {
            if (pd.OwnerClientId == ownerClientId) continue;
            if (pd.SpeedMultiplier.Value > fastestSpeed)
            {
                fastestSpeed = pd.SpeedMultiplier.Value;
                fastestId    = pd.OwnerClientId;
            }
        }

        if (fastestId == ulong.MaxValue) return;

        var targetPd = PlayerData.GetPlayer(fastestId);
        if (targetPd != null)
            targetPd.SpeedMultiplier.Value -= HangedSpeedPenalty.Value;
    }

    #endregion
}