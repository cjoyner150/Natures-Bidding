using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerVisualEffectManager : NetworkBehaviour
{
    PlayerContext ctx;
    [SerializeField] GameObject hitReactParticle;
    [SerializeField] GameObject deflectParticle;
    [SerializeField] GameObject parryParticle;
    [SerializeField] GameObject attackParticle;

    // Locally call event from anywhere in normal code with the clientId
    public static Action<ulong> SpawnHitReactionEffectsOnPlayer;

    public override void OnNetworkSpawn()
    {
        ctx = GetComponent<PlayerNetworkBehavior>().ctx;

        SpawnHitReactionEffectsOnPlayer += OnSpawnHitReactionEffectsOnPlayer;
    }

    public override void OnNetworkDespawn()
    {
        SpawnHitReactionEffectsOnPlayer -= OnSpawnHitReactionEffectsOnPlayer;
    }

    public void OnSpawnHitReactionEffectsOnPlayer(ulong clientId)
    {
        if (OwnerClientId == clientId)
        {
            SpawnHitReactParticles();
        }
        else
        {
            SpawnHitReactionEffectsClientRpc(clientId);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Everyone)]
    public void SpawnHitReactionEffectsClientRpc(ulong clientId)
    {
        if (OwnerClientId == clientId)
        {
            SpawnHitReactParticles();
        } 
    }

    private async void SpawnHitReactParticles()
    {
        hitReactParticle.SetActive(true);

        await UniTask.Delay(1000);

        hitReactParticle.SetActive(false);
    }
    private async void SpawnParryParticles()
    {
        hitReactParticle.SetActive(true);

        await UniTask.Delay(1000);

        hitReactParticle.SetActive(false);
    }
    private async void SpawnDeflectParticles()
    {
        deflectParticle.SetActive(true);

        await UniTask.Delay(1000);

        deflectParticle.SetActive(false);
    }
    private async void SpawnAttackParticles()
    {
        attackParticle.SetActive(true);

        await UniTask.Delay(1000);

        attackParticle.SetActive(false);
    }
}

