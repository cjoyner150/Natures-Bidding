using TMPro;
using Unity.Services.Authentication;
using UnityEngine;
using UnityUtils;

public class PlayerGameplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    private ulong clientId;
    public async void Initialize(ulong _clientId)
    {
        clientId = _clientId;

        string playerName = await CombatServerHandler.Instance.RequestPlayerNameByClientId(clientId);
        playerName = playerName.Split('#')[0];

        playerNameText.text = playerName;
        
    }
}
