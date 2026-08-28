using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UI;
using UnityUtils;

public class PlayerGameplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private RawImage playerImg;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite[] coloredSprites;
    private RenderTexture renderTexture;
    Camera renderCam;
    private ulong clientId;
    public async void Initialize(ulong _clientId)
    {
        clientId = _clientId;

        string playerName = await CombatServerHandler.Instance.RequestPlayerNameByClientId(clientId);
        playerName = playerName.Split('#')[0];

        playerNameText.text = playerName;

        renderTexture = new RenderTexture(512, 512, 5, RenderTextureFormat.ARGB32);

        renderCam = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<PlayerNetworkBehavior>().RenderCamera;
        renderCam.targetTexture = renderTexture;
        renderCam.enabled = true;
        playerImg.texture = renderTexture;

        var playerData = PersistentPlayerRegistry.Instance.GetByClientId(clientId);
        int idx = 0;

        if (playerData != null) idx = PersistentPlayerRegistry.Instance.GetByClientId(clientId).playerIndex;
        else GameLogger.Log(LogSeverity.Error, $"No player data found for player: {playerData}");

        backgroundImage.sprite = coloredSprites[idx];

        GameLogger.Log(LogSeverity.Verbose, $"rendercam is named: {renderCam.name} and is enabled: {renderCam.enabled} on init");
    }

    private void OnDestroy()
    {
        GameLogger.Log(LogSeverity.Verbose, $"Player UI is destroyed");

        if (renderCam != null)
            renderCam.enabled = false;

        if (renderTexture != null) 
            Destroy(renderTexture);
    }
}
