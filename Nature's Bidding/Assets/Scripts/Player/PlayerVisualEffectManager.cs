using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerVisualEffectManager : MonoBehaviour
{
    PlayerContext ctx;
    [SerializeField] GameObject hitReactParticle;
    [SerializeField] GameObject explosionParticle;

    public void Awake()
    {
        ctx = GetComponent<PlayerNetworkBehavior>().ctx;
    }

    public void SpawnExplosionParticles(Vector3 spawnPos)
    {
        GameObject go = Instantiate(explosionParticle, spawnPos, Quaternion.identity);
        SafeDispose(go, 2000).Forget();
    }

    public void SpawnHitReactParticles()
    {
        GameObject go = Instantiate(hitReactParticle, transform);
        SafeDispose(go, 1000).Forget();
    }

    private static async UniTask SafeDispose(GameObject obj, int milliseconds)
    {
        await UniTask.Delay(milliseconds);

        if (obj != null && !obj.IsDestroyed()) Destroy(obj);
    }
}

