using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    public NetworkVariable<float> health =  new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> maxHealth = new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isInvulnerable = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isParrying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isRoundWinner = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    PlayerContext ctx;
    NetworkObject selfNetworkObject;
    private IGameServerHandler _serverHandler;
    private PlayerGameplayUI playerGameplayUI;
    private MMProgressBar healthProgressBarVisual;

    bool isDead = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _serverHandler = FindAnyObjectByType<LobbyServerHandler>();
        _serverHandler ??= FindAnyObjectByType<CombatServerHandler>();
        selfNetworkObject = GetComponent<NetworkObject>();
        ctx = GetComponent<PlayerNetworkBehavior>()?.ctx;
        CombatServerHandler.OnCombatBegin.AddListener(OnCombatBegin);
        ctx.playerHealth = this;
    }

    public void OnCombatBegin()
    {
        playerGameplayUI = GameplaySpawnManager.Instance.SpawnPlayerHealthBar();
        playerGameplayUI.Initialize(selfNetworkObject.OwnerClientId);
        healthProgressBarVisual = playerGameplayUI.gameObject.GetComponentInChildren<MMProgressBar>();

        health.OnValueChanged += OnHealthChanged;
        maxHealth.OnValueChanged += OnMaxHealthChanged;

        if (IsOwner)
        {
            SendMaxHealthToServerRpc(ctx.playerStats.MaxHealth);
        }

        if (IsServer && IsOwner)
        {
            // Host sets their own health immediately since they are both owner and server
            maxHealth.Value = ctx.playerStats.MaxHealth;
            health.Value = ctx.playerStats.MaxHealth;
            healthProgressBarVisual.SetBar01(1f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendMaxHealthToServerRpc(float maxHealthValue)
    {
        maxHealth.Value = maxHealthValue;
        health.Value = maxHealthValue;
    }

    public override void OnNetworkDespawn()
    {
        if (playerGameplayUI != null && playerGameplayUI.gameObject != null)
        {
            Destroy(playerGameplayUI.gameObject);
        }

        base.OnNetworkDespawn();

        health.OnValueChanged -= OnHealthChanged;
        maxHealth.OnValueChanged -= OnMaxHealthChanged;
        CombatServerHandler.OnCombatBegin.RemoveListener(OnCombatBegin);

    }

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context)
    {
        if (!isInvulnerable.Value && !isParrying.Value)
        {
            context = IDamageable.HitCallbackContext.success;
            
            _serverHandler.RequestHitPlayerServerRpc(fromPlayerId, selfNetworkObject.OwnerClientId, damage);
        }
        else if (isParrying.Value)
        {
            context = IDamageable.HitCallbackContext.parried;
        }
        else
        {
            context = IDamageable.HitCallbackContext.failed;
        }
    }

    public void BeginParry()
    {
        isParrying.Value = true;
    }

    public void EndParry()
    {
        isParrying.Value = false;
    }

    private void OnHealthChanged(float from, float to)
    {
        if (isDead) return;
        healthProgressBarVisual.SetBar01(Mathf.Clamp01(to / maxHealth.Value));
        if (to <= 0 && IsServer)
        {
            isDead = true;
            _serverHandler?.OnPlayerDeath(selfNetworkObject.OwnerClientId);
            selfNetworkObject.Despawn();
        }
    }

    private void OnMaxHealthChanged(float from, float to)
    {
        if (healthProgressBarVisual == null) return;
        if (to > 0) healthProgressBarVisual.SetBar01(Mathf.Clamp01(health.Value / to));
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void PlayerDamagedFeedbackClientRpc(Vector3 fromPosition)
    {
        // Call event with your own client id to spawn it on yourself, or you could use another client id to spawn on another player
        PlayerVisualEffectManager.SpawnHitReactionEffectsOnPlayer?.Invoke(OwnerClientId);
        
        if (!IsOwner) return;
        ctx.lastHitFromPosition = fromPosition;
        ctx.shouldTakeKnockback = true;
        Debug.Log($"I've been hit! New health is {health.Value}");
    }

    public async void OnWinRound(int victoryLapDelay)
    {
        Destroy(playerGameplayUI.gameObject);

        if (!IsOwner) return;

        isInvulnerable.Value = true;

        await UniTask.Delay(victoryLapDelay);

        ctx.allowInputs = false;
    }

    public PlayerContext GetPlayerContext() => ctx;
    
}
