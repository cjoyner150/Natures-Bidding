using Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    public Color[] colors;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public PlayerContext ctx;
    private PlayerInputManager playerInput;
    private PlayerWeaponManager playerWeaponManager;
    private PlayerStatusEffectManager playerStatusEffectManager;
    private CinemachineTargetGroup cameraTargetGroup;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        cameraTargetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        cameraTargetGroup?.AddMember(transform, 1, 10);

        skinnedMeshRenderer.materials[2].color = colors[OwnerClientId];

        if (IsOwner)
        {

            var statsMediator = new StatsMediator();
            ctx.playerStats = new Stats(statsMediator, ctx.BaseStats, PersistentPlayerRegistry.Instance.GetByClientId(OwnerClientId));

            playerInput = gameObject.AddComponent<PlayerInputManager>();

            if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat)
            {
                playerStatusEffectManager = gameObject.AddComponent<PlayerStatusEffectManager>();

                playerWeaponManager = GetComponent<PlayerWeaponManager>();
                if (playerWeaponManager == null)
                {
                    Debug.LogError("[PlayerNetworkBehavior] Player Weapon Manager is null.");
                }

                playerWeaponManager.Initialize(playerStatusEffectManager);
                playerStatusEffectManager.Initialize(ctx.playerStats, OwnerClientId);
            }
            
            playerInput.InitializePlayer(ctx);

            
            ctx.maxJumps = ctx.playerStats.Jumps;
            transform.localScale *= ctx.playerStats.Size;

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