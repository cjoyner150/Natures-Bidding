using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PlayerInventoryPanel — One quarter of the inventory screen per player.
/// Shows their name, coins, all stats, and full item/upgrade list.
///
/// Prefab layout:
///   Root
///   ├── PanelBackground         Image
///   ├── LocalPlayerBorder       Image (only active for local player)
///   │
///   ├── Header                  Horizontal Layout Group
///   │   ├── PlayerNameText      TMP_Text — large
///   │   └── CoinsText           TMP_Text — gold
///   │
///   ├── StatsPanel              Vertical Layout Group
///   │   ├── StatsTitle          TMP_Text — "Stats"
///   │   ├── SpeedText           TMP_Text
///   │   ├── JumpText            TMP_Text
///   │   ├── DamageText          TMP_Text
///   │   ├── DefenceText         TMP_Text
///   │   └── HealthText          TMP_Text
///   │
///   ├── ItemsPanel              Vertical Layout Group
///   │   ├── ItemsTitle          TMP_Text — "Items"
///   │   └── ItemsContainer      Vertical Layout Group — rows spawn here
///   │
///   └── UpgradesPanel           Vertical Layout Group
///       ├── UpgradesTitle       TMP_Text — "Upgrades"
///       └── UpgradesContainer   Vertical Layout Group — rows spawn here
/// </summary>
public class PlayerInventoryPanel : MonoBehaviour
{
    #region Inspector Fields

    [Header("Panel Identity")]
    public Image    panelBackground;
    public Image    localPlayerBorder;
    public Color    localColor  = new Color(0.18f, 0.14f, 0.30f, 1f);
    public Color    remoteColor = new Color(0.09f, 0.08f, 0.13f, 1f);

    [Header("Header")]
    public TMP_Text playerNameText;
    public TMP_Text coinsText;

    [Header("Stats")]
    public TMP_Text speedText;
    public TMP_Text jumpText;
    public TMP_Text damageText;
    public TMP_Text defenceText;
    public TMP_Text healthText;

    [Header("Items")]
    public Transform  itemsContainer;
    public GameObject rowPrefab;         // Simple TMP_Text prefab for list rows

    [Header("Upgrades")]
    public Transform upgradesContainer;

    #endregion

    #region Private

    private ulong      _clientId;
    private PlayerData _playerData;

    #endregion

    #region Setup

    public void Initialise(ulong clientId, bool isLocal)
    {
        gameObject.SetActive(true);

        _clientId = clientId;

        if (panelBackground)   panelBackground.color = isLocal ? localColor : remoteColor;
        if (localPlayerBorder) localPlayerBorder.gameObject.SetActive(isLocal);

        _playerData = PlayerData.GetPlayer(clientId);
        if (_playerData != null)
            Populate();
        else
            StartCoroutine(RetryPopulate());
    }

    IEnumerator RetryPopulate()
    {
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForSeconds(0.2f);
            _playerData = PlayerData.GetPlayer(_clientId);
            if (_playerData != null) { Populate(); yield break; }
        }
        Debug.LogWarning($"[PlayerInventoryPanel] Could not find PlayerData for client {_clientId}");
    }

    #endregion

    #region Populate

    void Populate()
    {
        if (_playerData == null) return;

        // Header
        if (playerNameText) playerNameText.text = _playerData.PlayerName.Value.Value;
        if (coinsText)      coinsText.text      = $"coins {_playerData.Coins.Value}";

        // Stats
        if (speedText)   speedText.text   = Stat("Speed",   _playerData.SpeedMultiplier.Value);
        if (jumpText)    jumpText.text    = Stat("Jump",    _playerData.JumpMultiplier.Value);
        if (damageText)  damageText.text  = Stat("Damage",  _playerData.DamageMultiplier.Value);
        if (defenceText) defenceText.text = Stat("Defence", _playerData.DefenseMultiplier.Value);
        if (healthText)  healthText.text  = Stat("Health",  1f + _playerData.MaxHealthBonus.Value);

        // Items list
        PopulateList(itemsContainer, _playerData.Items);

        // Upgrades list
        var upgradeLines = new List<string>();
        foreach (var kvp in _playerData.UpgradeCounts)
            upgradeLines.Add($"{kvp.Key} ×{kvp.Value}");

        if (upgradeLines.Count == 0) upgradeLines.Add("None");
        PopulateList(upgradesContainer, upgradeLines);
    }

    void PopulateList(Transform container, IEnumerable<string> items)
    {
        if (container == null) return;
        foreach (Transform child in container) Destroy(child.gameObject);

        bool any = false;
        foreach (var item in items)
        {
            any = true;
            if (rowPrefab == null) continue;
            var go    = Instantiate(rowPrefab, container);
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label) label.text = item;
        }

        if (!any)
        {
            if (rowPrefab == null) return;
            var go    = Instantiate(rowPrefab, container);
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label) label.text = "None";
        }
    }

    string Stat(string label, float multiplier)
    {
        int pct = Mathf.RoundToInt((multiplier - 1f) * 100f);
        return pct > 0
            ? $"{label}: <color=#4caf7d>+{pct}%</color>"
            : $"{label}: Base";
    }

    #endregion
}