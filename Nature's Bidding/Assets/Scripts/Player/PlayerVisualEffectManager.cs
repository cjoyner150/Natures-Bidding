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

    GameObject batConfusionEffectCache;
    GameObject starEffectCache;

    public void SpawnSlashEffectParticles(int milliseconds)
    {
        //GameObject go = Instantiate(explosionParticle, weaponHolderTransform, false);
        //go.transform.localPosition = Vector3.zero;
        //go.transform.localRotation = Quaternion.identity;

        //SafeDispose(go, milliseconds).Forget();
    }

    public void SpawnParryEffectParticles(int milliseconds)
    {
        //GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        //go.transform.localPosition = Vector3.zero;
        //SafeDispose(go, milliseconds).Forget();
    }

    public void SpawnParrySuccessReactionParticles()
    {
        //GameObject go = Instantiate(hitReactParticle, weaponHolderTransform);
        //SafeDispose(go, 1000).Forget();
    }

    public void SpawnHitReactParticles(bool critical)
    {
        //if (critical)
        //{
        //    GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        //    go.transform.localPosition = Vector3.zero;
        //    SafeDispose(go, 1000).Forget();
        //}
        //else
        //{
        //    GameObject go = Instantiate(hitReactParticle, gameObject.transform, false);
        //    go.transform.localPosition = Vector3.zero;
        //    SafeDispose(go, 1000).Forget();
        //}
    }

    public void SpawnDashParticles()
    {
        //GameObject go = Instantiate(hitReactParticle, gameObject.transform, false);
        //go.transform.localPosition = Vector3.zero;
        //SafeDispose(go, 1000).Forget();
    }

    public void SpawnTeleportParticles()
    {
        //GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        //go.transform.localPosition = Vector3.zero;
        //go.transform.SetParent(null, true);
        //SafeDispose(go, 1000).Forget();
    }

    public void ToggleStarParticles(bool enabled)
    {
        if (enabled)
        {
            GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
            go.transform.localPosition = Vector3.zero;
            
            if (starEffectCache != null)
            {
                SafeDispose(starEffectCache, 0).Forget();
            }

            starEffectCache = go;
        }
        else
        {
            SafeDispose(starEffectCache, 0).Forget();
        }
    }

    public void SpawnJumpParticles()
    {
        //GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        //go.transform.localPosition = Vector3.zero;
        //go.transform.SetParent(null);
        //SafeDispose(go, 1000).Forget();
    }

    public void SpawnExplosionParticles(Vector3 spawnPos)
    {
        //GameObject go = Instantiate(explosionParticle, spawnPos, Quaternion.identity);
        //SafeDispose(go, 2000).Forget();
    }

    public void SpawnStunParticles(int milliseconds)
    {
        //GameObject go = Instantiate(hitReactParticle, gameObject.transform, false);
        //go.transform.localPosition = Vector3.zero;
        //SafeDispose(go, milliseconds).Forget();
    }

    public void SpawnConfettiParticles()
    {
        GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        SafeDispose(go, 5000).Forget();
    }

    public void SpawnBatConfusionParticles()
    {
        GameObject go = Instantiate(explosionParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;

        batConfusionEffectCache = go;
    }

    public void RemoveBatConfusionParticles()
    {
        SafeDispose(batConfusionEffectCache, 0).Forget();
    }

    private static async UniTask SafeDispose(GameObject obj, int milliseconds)
    {
        await UniTask.Delay(milliseconds);

        if (obj != null && !obj.IsDestroyed()) Destroy(obj);
    }
}

