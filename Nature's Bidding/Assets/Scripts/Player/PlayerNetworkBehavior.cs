using Cinemachine;
using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityUtils;

public class PlayerNetworkBehavior : NetworkBehaviour
{
    public Camera RenderCamera;
    public Material sunMat;
    public Color[] colors;
    public SkinnedMeshRenderer skinnedMeshRenderer;
    public PlayerContext ctx;
    private PlayerInputManager playerInput;
    private PlayerWeaponManager playerWeaponManager;
    private PlayerStatusEffectManager playerStatusEffectManager;
    private PlayerMaskVisualManager playerMaskVisualManager;
    private CinemachineTargetGroup cameraTargetGroup;

    public override void OnNetworkSpawn()
    {


        base.OnNetworkSpawn();

        cameraTargetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        cameraTargetGroup?.AddMember(transform, 1, 4);

        if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat)
        {
            playerMaskVisualManager = GetComponent<PlayerMaskVisualManager>();
            playerMaskVisualManager.Initialize(OwnerClientId);
        }

        SyncAllPlayerColors();

        if (IsOwner)
        {
            var statsMediator = new StatsMediator();
            ctx.playerStats = new Stats(statsMediator, ctx.BaseStats, PersistentPlayerRegistry.Instance.GetByClientId(OwnerClientId));

            playerInput = gameObject.AddComponent<PlayerInputManager>();

            if (PersistentGameStateManager.Instance.State == PersistentGameStateManager.GameState.Combat)
            {
                playerStatusEffectManager = gameObject.AddComponent<PlayerStatusEffectManager>();
                playerWeaponManager = GetComponent<PlayerWeaponManager>();
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

    private async void SyncAllPlayerColors()
    {
        await UniTask.WaitUntil(() => PersistentPlayerRegistry.Instance.GetByClientId(OwnerClientId) != null);

        var myData = PersistentPlayerRegistry.Instance.GetByClientId(OwnerClientId);
        if (skinnedMeshRenderer == null) return;

        skinnedMeshRenderer.materials[2].SetColor("_Tint", colors[myData.playerIndex]);

        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClientsList.All(p => p.PlayerObject != null)
        );

        var players = PersistentPlayerRegistry.Instance.GetAllPlayers().Where(p => p.clientId != OwnerClientId);

        foreach ( var player in players )
        {
            var playerNetworkBehavior = NetworkManager.Singleton.ConnectedClients[player.clientId].PlayerObject.GetComponent<PlayerNetworkBehavior>();
            playerNetworkBehavior.skinnedMeshRenderer.materials[2].SetColor("_Tint", colors[player.playerIndex]);
        }

    }

    public void SwapMaterialOnPlayer()
    {
        ApplyMaterialSwap();
        NotifyPlayerMaterialSwapClientRpc();
    }

    [Rpc(SendTo.NotMe, InvokePermission = RpcInvokePermission.Owner)]
    public void NotifyPlayerMaterialSwapClientRpc()
    {
        ApplyMaterialSwap();
    }

    private void ApplyMaterialSwap()
    {
        var mats = skinnedMeshRenderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            mats[i] = sunMat;
        }
        skinnedMeshRenderer.materials = mats;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + (transform.up * .125f), .2f);
    }
}