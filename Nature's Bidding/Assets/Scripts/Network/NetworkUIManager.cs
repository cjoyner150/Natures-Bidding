using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class NetworkUIManager : MonoBehaviour
{
    [SerializeField] GameObject startPanel;
    private NetworkSessionManager sessionManager;

    private void Awake()
    {
        sessionManager = NetworkSessionManager.Instance;
    }

    public async void JoinSessionByButton(TMP_InputField input)
    {
        bool validSession = await sessionManager.JoinSessionByCode(input.text);
        if (validSession)
        {
            startPanel.SetActive(false);
        }
    }

    public void QuickJoinByButton()
    {
        _ = sessionManager.QuickJoin();
    }

    public void StartSessionAsHostByButton()
    {
        _ = sessionManager.StartSessionAsHost();
    }

    public void EnterNameByButton(TMP_InputField input)
    {
        _ = sessionManager.ChangePlayerName(input.text);
    }
}
