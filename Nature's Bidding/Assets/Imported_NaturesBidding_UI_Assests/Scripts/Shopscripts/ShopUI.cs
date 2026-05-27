using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ShopUI — Thin UI controller for the shop phase screen.
/// Delegates all game logic to ShopManager.Instance.
/// Attach to the ShopCanvas GameObject.
/// </summary>
public class ShopUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("Phase Info")]
    public TMP_Text phaseTitleText;
    public TMP_Text roundInfoText;

    [Header("Host Controls")]
    public Button nextRoundButton;
    public Button endGameButton;

    [Header("Player Info Bar")]
    public LocalMoneyUI localMoneyUI;

    #endregion

    #region Lifecycle

    void OnEnable()
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        nextRoundButton?.gameObject.SetActive(isHost);
        endGameButton?.gameObject.SetActive(isHost);

        if (phaseTitleText) phaseTitleText.text = "Shop Phase";
        if (roundInfoText)  roundInfoText.text  = "Browse upgrades below.";
    }

    #endregion

    #region Button Callbacks

    public void OnNextRound()
    {
        if (ShopManager.Instance == null)
        {
            Debug.LogWarning("[ShopUI] ShopManager.Instance is null.");
            return;
        }
        ShopManager.Instance.OnBackToBidding();
    }

    public void OnEndGame()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            Debug.Log("[ShopUI] End game — load your results scene here.");
    }

    #endregion

    #region Info Refresh

    public void RefreshInfo(int roundNumber, int totalItems)
    {
        if (roundInfoText)
            roundInfoText.text = $"Round {roundNumber} • {totalItems} items in play";
    }

    #endregion
}