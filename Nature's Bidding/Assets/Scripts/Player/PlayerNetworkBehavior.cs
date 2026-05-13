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
            RegisterPlayer();
        }

        cameraTargetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        if (cameraTargetGroup != null)
        {
            cameraTargetGroup.AddMember(transform, 1, 10);
        }

        skinnedMeshRenderer.materials[2].color = colors[OwnerClientId];

    }

    async void RegisterPlayer()
    {
        await UniTask.DelayFrame(1);
        GameplayServerHandler.Instance.RegisterPlayerOnServerRpc(new PlayerServerInfo(OwnerClientId, AuthenticationService.Instance.PlayerId, AuthenticationService.Instance.PlayerName));
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (IsOwner)
        {
            GameplayServerHandler.OnPlayerRegistered.RemoveListener(OnPlayerRegistered);
        }
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
