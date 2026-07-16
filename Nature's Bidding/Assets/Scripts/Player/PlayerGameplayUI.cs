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

        Debug.Log($"[PlayerGameplayUI] rendercam is named: {renderCam.name} and is enabled: {renderCam.enabled} on init");
    }

    private void Update()
    {
        Debug.Log($"[PlayerGameplayUI] rendercam is named: {renderCam.name} and is enabled: {renderCam.enabled}");
    }

    private void OnDestroy()
    {
        Debug.Log($"[PlayerGameplayUI] ui is destroyed");

        if (renderCam != null)
            renderCam.enabled = false;

        if (renderTexture != null) 
            Destroy(renderTexture);
    }
}
