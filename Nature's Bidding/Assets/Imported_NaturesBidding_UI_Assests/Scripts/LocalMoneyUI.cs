using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// LocalMoneyUI — Displays the local player's coin balance, item count, and name.
/// Attach to any UI GameObject. Polls on a timer rather than every frame.
/// </summary>
public class LocalMoneyUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("UI References")]
    public TMP_Text coinsText;
    public TMP_Text itemCountText;
    public TMP_Text playerNameText;

    [Header("Settings")]
    public float refreshInterval = 0.5f;

    #endregion

    #region Private State

    private float               _refreshTimer;
    private PlayerShoppingNetworkBehavior _localPlayerData;

    #endregion

    #region Update

    void Update()
    {
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = refreshInterval;

        if (_localPlayerData == null)
        {
            _localPlayerData = NetworkManager.Singleton != null
                ? PlayerShoppingNetworkBehavior.GetPlayer(NetworkManager.Singleton.LocalClientId)
                : null;

            if (_localPlayerData == null) { SetDisplays("—", "—", "—"); return; }
        }

        RefreshDisplay();
    }

    #endregion

    #region Display

    void RefreshDisplay()
    {
        if (_localPlayerData == null) return;
        if (coinsText)      coinsText.text      = $"Coins: {_localPlayerData.Coins.Value}";
        if (itemCountText)  itemCountText.text   = $"Items: {_localPlayerData.Items.Count}";
        if (playerNameText) playerNameText.text  = _localPlayerData.PlayerName.Value.Value;
    }

    void SetDisplays(string coins, string items, string name)
    {
        if (coinsText)      coinsText.text      = $"Coins: {coins}";
        if (itemCountText)  itemCountText.text   = $"Items: {items}";
        if (playerNameText) playerNameText.text  = name;
    }

    #endregion
}