using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BiddingControlPanel : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI bidAmountText;
    [SerializeField] TextMeshProUGUI totalGoldText;

    [SerializeField] Image bidUpArrowImage;
    [SerializeField] Image bidDownArrowImage;

    [SerializeField] Sprite[] arrowUpSprites;
    [SerializeField] Sprite[] arrowDownSprites;

    int idx;

    public void InitializeAsLocalPlayer(int playerIndex, out TextMeshProUGUI bidText, out TextMeshProUGUI goldText)
    {
        idx = playerIndex;
        bidText = bidAmountText;
        goldText = totalGoldText;

        bidAmountText.text = "0";

        InitArrowSprites(playerIndex);
    }

    public void InitializeAsEnemyPlayer(int playerIndex)
    {
        idx = playerIndex;
        bidAmountText.text = "?";
        bidAmountText.fontStyle = FontStyles.Normal;
        totalGoldText.text = PersistentPlayerRegistry.Instance.GetAllPlayers().Find(p => p.playerIndex == playerIndex)?.gold.ToString() ?? "NA";

        InitArrowSprites(playerIndex);
    }

    private void InitArrowSprites(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < arrowUpSprites.Length && playerIndex < arrowDownSprites.Length)
        {
            bidUpArrowImage.sprite = arrowUpSprites[playerIndex];
            bidDownArrowImage.sprite = arrowDownSprites[playerIndex];
        }
        else
        {
            GameLogger.Log(LogSeverity.Warning, $"Player index {playerIndex} is out of range for arrow sprites.");
        }
    }

    public void UpdateGold()
    {
        totalGoldText.text = PersistentPlayerRegistry.Instance.GetAllPlayers().Find(p => p.playerIndex == idx)?.gold.ToString() ?? "NA";
    }
}
