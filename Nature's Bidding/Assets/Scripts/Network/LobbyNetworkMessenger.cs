using Unity.Netcode;
using UnityEngine;

public class LobbyNetworkMessenger : NetworkSingleton<LobbyNetworkMessenger>
{
    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    [Rpc(SendTo.Server)]
    public void SendAuthToServerRpc(string authId, string playerName, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        PlayerRegistry.Instance.Register(clientId, authId, playerName);

        if (GameplayServerHandler.Instance != null)
            GameplayServerHandler.Instance.SpawnAndRegisterPlayer(clientId);
    }
}