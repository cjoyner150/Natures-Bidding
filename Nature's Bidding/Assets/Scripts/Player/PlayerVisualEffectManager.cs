using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerVisualEffectManager : MonoBehaviour
{
    [Header("Particle Prefabs")]
    [SerializeField] GameObject hitReactParticle;
    [SerializeField] GameObject explosionParticle;

    [Header("References")]
    [SerializeField] Transform weaponHolderTransform;

    public void SpawnSlashEffectParticles()
    {
        GameObject go = Instantiate(explosionParticle, weaponHolderTransform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        SafeDispose(go, 1000).Forget();
    }

    public void SpawnParryEffectParticles()
    {
        GameObject go = Instantiate(explosionParticle, transform);
        SafeDispose(go, 1000).Forget();
    }

    public void SpawnParrySuccessReactionParticles()
    {
        GameObject go = Instantiate(hitReactParticle, transform);
        SafeDispose(go, 1000).Forget();
    }

    public void SpawnHitReactParticles(bool critical)
    {
        if (critical)
        {
            // Do something else here
            GameObject go = Instantiate(hitReactParticle, transform);
            SafeDispose(go, 1000).Forget();
        }
        else
        {
            GameObject go = Instantiate(hitReactParticle, transform);
            SafeDispose(go, 1000).Forget();
        }
    }

    public void SpawnDashParticles()
    {

    }

    public void SpawnJumpParticles()
    {

    }

    public void SpawnLifestealParticles()
    {

    }

    public void SpawnExplosionParticles(Vector3 spawnPos)
    {
        GameObject go = Instantiate(explosionParticle, spawnPos, Quaternion.identity);
        SafeDispose(go, 2000).Forget();
    }

    private static async UniTask SafeDispose(GameObject obj, int milliseconds)
    {
        await UniTask.Delay(milliseconds);

        if (obj != null && !obj.IsDestroyed()) Destroy(obj);
    }
}

