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
    public NetworkVariable<bool> isStunned = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

            NotifyParrySuccessClientRpc(fromPlayerId);

            context = IDamageable.HitCallbackContext.parried;
        }
        else
        {
            context = IDamageable.HitCallbackContext.failed;
        }
    }

    public void TickHealth(float damage, ulong damageCreditId)
    {
        if (!isInvulnerable.Value)
        {
            var combatHandler = _serverHandler as CombatServerHandler;
            if (combatHandler == null)
            {
                Debug.LogError("[PlayerHealth] TickHealth: _serverHandler is not a CombatServerHandler!");
                return;
            }

            combatHandler.RequestTickPlayerHealthServerRpc(selfNetworkObject.OwnerClientId, damageCreditId, damage);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyParrySuccessClientRpc(ulong attackerId)
    {

        PlayerCombatHooks.TriggerOnParry(attackerId);
        ctx.parryResponse = true;
    }

    public void StunPlayer(float additionalStunTime)
    {
        if (!IsOwner)
        {
            RequestStunPlayerServerRpc(additionalStunTime, OwnerClientId);
        }
        else
        {
            if (!isStunned.Value)
                NetworkVisualEffectManager.SpawnParrySuccessReactEffectsOnPlayer?.Invoke(OwnerClientId);

            ctx.additionalStunTime += additionalStunTime;

            ctx.combo = 0;
            ctx.shouldStunSelf = true;
            isStunned.Value = true;
        }
        
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestStunPlayerServerRpc(float additionalStunTime, ulong targetClientId)
    {
        NotifyStunPlayerClientRpc(additionalStunTime, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyStunPlayerClientRpc(float additionalStunTime, RpcParams _params)
    {
        StunPlayer(additionalStunTime);
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
        healthProgressBarVisual.SetBar01(Mathf.Clamp01(to / maxHealth.Value));
    }


    private UniTaskCompletionSource _deathAckTcs;
    private UniTaskCompletionSource _killAckTcs;

    public UniTask NotifyDeathAndAwaitAck(ulong killCreditId)
    {
        _deathAckTcs = new UniTaskCompletionSource();
        NotifyPlayerDeadClientRpc(killCreditId);
        return _deathAckTcs.Task;
    }

    public UniTask NotifyKillCreditAndAwaitAck(ulong victimId)
    {
        _killAckTcs = new UniTaskCompletionSource();
        NotifyPlayerKillCreditClientRpc(victimId);
        return _killAckTcs.Task;
    }


    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyPlayerDeadClientRpc(ulong killCreditId)
    {
        PlayerCombatHooks.TriggerOnDeath(killCreditId);
        AckDeathProcessedServerRpc();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void NotifyPlayerKillCreditClientRpc(ulong victimId)
    {
        PlayerCombatHooks.TriggerOnKill(victimId);
        AckKillCreditProcessedServerRpc();
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AckDeathProcessedServerRpc()
    {
        _deathAckTcs?.TrySetResult();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AckKillCreditProcessedServerRpc()
    {
        _killAckTcs?.TrySetResult();
    }

    private void OnMaxHealthChanged(float from, float to)
    {
        if (healthProgressBarVisual == null) return;
        if (to > 0) healthProgressBarVisual.SetBar01(Mathf.Clamp01(health.Value / to));
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void PlayerDamagedFeedbackClientRpc(Vector3 fromPosition, ulong fromAttackerId, bool critical = false)
    {
        NetworkVisualEffectManager.SpawnHitReactionEffectsOnPlayer?.Invoke(OwnerClientId, critical);
        
        if (!IsOwner) return;
        ctx.lastHitFromPosition = fromPosition;
        ctx.shouldTakeKnockback = true;
        PlayerCombatHooks.TriggerOnHit(fromAttackerId);
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
