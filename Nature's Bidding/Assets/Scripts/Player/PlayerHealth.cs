using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    public NetworkVariable<float> health =  new(100, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isInvulnerable = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isParrying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    NetworkObject selfNetworkObject;
    GameplayServerHandler gameplayServerHandler;
    private PlayerGameplayUI playerGameplayUI;
    private MMProgressBar healthProgressBarVisual;
    
    public override void OnNetworkSpawn()
    {
        gameplayServerHandler = FindAnyObjectByType<GameplayServerHandler>();
        selfNetworkObject = GetComponent<NetworkObject>();
        
        TestingGameManager.OnSessionStarted.AddListener(OnSessionStarted);
    }

    public void OnSessionStarted()
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
        TestingGameManager.OnSessionStarted.RemoveListener(OnSessionStarted);
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
        if (IsServer) return;
        
        healthProgressBarVisual.SetBar01((health.Value / 100f));
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void PlayerDamagedFeedbackClientRpc()
    {
        if (!IsOwner) return;
        
        Debug.Log($"{selfNetworkObject.OwnerClientId}: I'm hit!");
        
    }
    
}
