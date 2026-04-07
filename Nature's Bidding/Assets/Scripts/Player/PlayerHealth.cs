using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    public NetworkVariable<float> health =  new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isInvulnerable = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isParrying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    PlayerContext ctx;
    NetworkObject selfNetworkObject;
    GameplayServerHandler gameplayServerHandler;
    private PlayerGameplayUI playerGameplayUI;
    private MMProgressBar healthProgressBarVisual;
    
    public override void OnNetworkSpawn()
    {
        gameplayServerHandler = FindAnyObjectByType<GameplayServerHandler>();
        selfNetworkObject = GetComponent<NetworkObject>();
        ctx = GetComponent<PlayerNetworkBehavior>()?.ctx;
        
        GameplayServerHandler.OnAllPlayersRegistered.AddListener(OnAllPlayersRegistered);
    }

    public void OnAllPlayersRegistered()
    {
        playerGameplayUI = TestingGameManager.Instance.SpawnPlayerHealthBar();
        playerGameplayUI.Initialize(selfNetworkObject.OwnerClientId);
        
        healthProgressBarVisual = playerGameplayUI.gameObject.GetComponentInChildren<MMProgressBar>();
        health.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            health.Value = 100;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        health.OnValueChanged -= OnHealthChanged;
        GameplayServerHandler.OnAllPlayersRegistered.RemoveListener(OnAllPlayersRegistered);
    }

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context)
    {
        if (!isInvulnerable.Value && !isParrying.Value)
        {
            context = IDamageable.HitCallbackContext.success;
            
            gameplayServerHandler.RequestHitPlayerServerRpc(fromPlayerId, selfNetworkObject.OwnerClientId, damage);
        }
        else
        {
            context = IDamageable.HitCallbackContext.failed;
        }
    }

    private void OnHealthChanged(float from, float to)
    {
        healthProgressBarVisual.SetBar01((health.Value / 100f));
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void PlayerDamagedFeedbackClientRpc(Vector3 fromPosition)
    {
        if (!IsOwner) return;
        
        ctx.lastHitFromPosition = fromPosition;
        ctx.shouldTakeKnockback = true;
        Debug.Log("I've been hit!");
    }
    
}
