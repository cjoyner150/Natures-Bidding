using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityUtils;

public class PlayerGameplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    private ulong clientId;
    public void Initialize(ulong _clientId)
    {
        clientId = _clientId;
        playerNameText.text = NetworkSessionManager.Instance.RequestPlayerNameByClientId(clientId);
        
    }

    void Update()
    {
        if (playerNameText.text.IsNullOrEmpty())
        {
            playerNameText.text = NetworkSessionManager.Instance.RequestPlayerNameByClientId(clientId);
        }
    }
}
