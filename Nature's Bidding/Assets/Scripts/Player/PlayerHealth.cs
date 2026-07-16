using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    public NetworkVariable<float> health =  new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> maxHealth = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isInvulnerable = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isParrying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isRoundWinner = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    PlayerContext ctx;
    NetworkObject selfNetworkObject;
    private IGameServerHandler _serverHandler;
    private PlayerGameplayUI playerGameplayUI;
    private MMProgressBar healthProgressBarVisual;

    private readonly Queue<float> pendingMaxHealthUpdates = new();
    private bool isProcessingHealthQueue = false;
    private bool isHealthInitialized = false;

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
            SendMaxHealthToServerRpc(ctx.playerStats.MaxHealth, OwnerClientId);
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
    public void SendMaxHealthToServerRpc(float maxHealthValue, ulong clientId)
    {
        pendingMaxHealthUpdates.Enqueue(maxHealthValue);

        if (!isProcessingHealthQueue)
            DrainMaxHealthQueue().Forget();
    }

    private async UniTaskVoid DrainMaxHealthQueue()
    {
        isProcessingHealthQueue = true;

        if (!IsSpawned)
        {
            await UniTask.WaitUntil(() => IsSpawned);
        }

        while (pendingMaxHealthUpdates.Count > 0)
        {
            float to = pendingMaxHealthUpdates.Dequeue();
            float from = maxHealth.Value;

            maxHealth.Value = to;

            if (!isHealthInitialized)
            {
                health.Value = to;
                isHealthInitialized = true;
            }
            else
            {
                health.Value += to - from;
            }

            Debug.Log($"[PlayerHealth] Health changed on client {OwnerClientId}. From: {from}, To: {to}, New max health: {maxHealth.Value}, New health: {health.Value}");
        }

        isProcessingHealthQueue = false;
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

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context, bool critical = false)
    {
        if (!isInvulnerable.Value && !isParrying.Value)
        {
            context = IDamageable.HitCallbackContext.success;
            
            _serverHandler.RequestHitPlayerServerRpc(fromPlayerId, selfNetworkObject.OwnerClientId, damage, critical);
        }
        else if (isParrying.Value)
        {
            Debug.Log("[PlayerHealth] Hit Parried");

            NotifyParrySuccessClientRpc();

            context = IDamageable.HitCallbackContext.parried;
        }
        else
        {
            context = IDamageable.HitCallbackContext.failed;
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyParrySuccessClientRpc()
    {
        ctx.parryResponse = true;
    }

    public void Heal(float amount)
    {
        if (!isDead && health.Value > 0)
            _serverHandler.RequestHealServerRpc(OwnerClientId, amount);
    }

    public void BeginParry()
    {
        Debug.Log("[PlayerHealth] Parry Begun");
        isParrying.Value = true;
    }

    public void EndParry()
    {
        Debug.Log("[PlayerHealth] Parry End");
        isParrying.Value = false;
    }

    public void Boom(float damage, float radius)
    {
        CombatServerHandler combatHandler = _serverHandler as CombatServerHandler;
        if (combatHandler == null) return;

        combatHandler.RequestPlayerBoomServerRpc(OwnerClientId, damage, radius);
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
    public void PlayerDamagedFeedbackClientRpc(Vector3 fromPosition, bool critical = false)
    {
        NetworkVisualEffectManager.SpawnHitReactionEffectsOnPlayer?.Invoke(OwnerClientId, critical);
        
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
