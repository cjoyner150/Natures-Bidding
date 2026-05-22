using Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    public Color[] colors;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public PlayerContext ctx;
    private PlayerInputManager playerInput;
    private PlayerStatusEffectManager playerStatusEffectManager;
    private CinemachineTargetGroup cameraTargetGroup;

    private Stats stats;
    private StatsMediator statsMediator;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        cameraTargetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        cameraTargetGroup?.AddMember(transform, 1, 10);

        skinnedMeshRenderer.materials[2].color = colors[OwnerClientId];

        if (IsOwner)
        {
            playerInput = gameObject.AddComponent<PlayerInputManager>();
            playerInput.InitializePlayer(ctx);

            statsMediator = new StatsMediator();
            stats = new Stats(statsMediator, ctx.BaseStats);

            playerStatusEffectManager = gameObject.AddComponent<PlayerStatusEffectManager>();
            playerStatusEffectManager.Initialize(stats, ctx.statusEffectsOnStart);

            if (LobbyServerHandler.Instance != null)
                LobbyServerHandler.OnPlayerRegistered.AddListener(OnPlayerRegistered);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsOwner)
        {
            LobbyServerHandler.OnPlayerRegistered.RemoveListener(OnPlayerRegistered);
            cameraTargetGroup?.RemoveMember(transform);
        }
    }

    private void OnPlayerRegistered()
    {
        if (!IsOwner) return;
        Debug.Log("I have been registered!");
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyRegisteredRpc(ulong clientId, int playerCount)
    {
        var data = PersistentPlayerRegistry.Instance.GetByClientId(clientId);
        Debug.Log($"Registered: clientId {clientId}, name {data?.playerName}. Total: {playerCount}");
        LobbyServerHandler.OnPlayerRegistered?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (transform.up * .125f), .2f);
    }
}