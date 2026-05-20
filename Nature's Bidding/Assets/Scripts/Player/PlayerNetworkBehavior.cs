using Cinemachine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    public Color[] colors;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public PlayerContext ctx;
    private PlayerInputManager playerInput;
    private CinemachineTargetGroup cameraTargetGroup;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            GameplayServerHandler.OnPlayerRegistered.AddListener(OnPlayerRegistered);
        }

        cameraTargetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        if (cameraTargetGroup != null)
        {
            cameraTargetGroup.AddMember(transform, 1, 10);
        }

        skinnedMeshRenderer.materials[2].color = colors[OwnerClientId];

    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (IsOwner)
        {
            GameplayServerHandler.OnPlayerRegistered.RemoveListener(OnPlayerRegistered);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyRegisteredRpc(PlayerServerInfo info, int playersCount)
    {
        GameplayServerHandler.OnPlayerRegistered?.Invoke();
        Debug.Log($"Registered: clientId {info.clientId}, name {info.playerName}. Total: {playersCount}");

        if (playersCount >= GameplayServerHandler.Instance.PlayersRequiredBeforeStart)
            GameplayServerHandler.Instance.AllPlayersRegisteredServerRpc();
    }

    public void OnPlayerRegistered()
    {
        if (IsOwner)
        {
            playerInput = gameObject.AddComponent<PlayerInputManager>();
            playerInput.InitializePlayer(ctx);

            Debug.Log("I have been registered!");
        }
        
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (transform.up * .125f), .2f);
    }
}
