using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.VisualScripting;
using MoreMountains.Feedbacks;
using UnityEngine;


public class PlayerVisualEffectManager : MonoBehaviour
{
    [Header("Particle Prefabs")]
    [SerializeField] GameObject hitReactParticle;
    [SerializeField] GameObject slashParticle;
    [SerializeField] GameObject starParticle;
    [SerializeField] GameObject confusionParticle;
    [SerializeField] GameObject parryParticle;
    [SerializeField] GameObject parrySuccessParticle;
    [SerializeField] GameObject stunParticle;
    [SerializeField] GameObject explosionParticle;
    [SerializeField] GameObject jumpParticle;
    [SerializeField] GameObject dashParticle;
    [SerializeField] GameObject teleportParticle;


    [Header("References")]
    [SerializeField] Transform weaponHolderTransform;
    [SerializeField] MMF_Player hitReactFeedback;
    

    GameObject batConfusionEffectCache;
    GameObject starEffectCache;
    Color playerColor = Color.white;

    private void Start()
    {
        InitializeColorWhenReady().Forget();
    }

    private async UniTaskVoid InitializeColorWhenReady()
    {
        var playerNetworkBehavior = GetComponent<PlayerNetworkBehavior>();
        await UniTask.WaitUntil(() => PersistentPlayerRegistry.Instance.GetByClientId(playerNetworkBehavior.OwnerClientId) != null);

        if (this == null) return;

        playerColor = playerNetworkBehavior.GetPlayerColor();
    }

    public void SpawnSlashEffectParticles(int milliseconds)
    {
        GameObject go = Instantiate(slashParticle, weaponHolderTransform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        SafeDispose(go, milliseconds).Forget();
    }

    public void SpawnParryEffectParticles(int milliseconds)
    {
        Debug.Log(milliseconds);
        GameObject go = Instantiate(parryParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        SafeDispose(go, milliseconds).Forget();
    }

    public void SpawnParrySuccessReactionParticles()
    {
        GameObject go = Instantiate(parrySuccessParticle, weaponHolderTransform);
        SafeDispose(go, 1000).Forget();
    }

    public void SpawnHitReactParticles(bool critical, Vector3 fromPos, float dmg)
    {
        //if (critical)
        //{
        //    GameObject go = Instantiate(hitReactParticle, gameObject.transform, false);
        //    go.transform.localPosition = Vector3.zero;
        //    SafeDispose(go, 1000).Forget();
        //    hitReactFeedback.PlayFeedbacks();
        //}

        Transform pTrans = gameObject.transform;
        Vector3 direction = fromPos - gameObject.transform.position;
        Quaternion rotation = Quaternion.LookRotation(direction);
        pTrans.rotation = rotation;

        GameObject go = Instantiate(hitReactParticle, pTrans, false);
        go.transform.localPosition = Vector3.zero;
        SafeDispose(go, 1000).Forget();
        hitReactFeedback.PlayFeedbacks();
        
    }

    public void SpawnDashParticles()
    {
        GameObject go = Instantiate(dashParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        SafeDispose(go, 1000).Forget();
    }

    public void SpawnTeleportParticles()
    {
        GameObject go = Instantiate(teleportParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.SetParent(null, true);
        SafeDispose(go, 1000).Forget();
    }

    public void ToggleStarParticles(bool enabled)
    {
        if (enabled)
        {
            GameObject go = Instantiate(starParticle, gameObject.transform, false);
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
        GameObject go = Instantiate(jumpParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.SetParent(null);
        SafeDispose(go, 1000).Forget();
    }

    public void SpawnExplosionParticles(Vector3 spawnPos)
    {
        Debug.Log("I've been told boom!");
        GameObject go = Instantiate(explosionParticle, spawnPos, Quaternion.identity);
        SafeDispose(go, 4000).Forget();
    }

    public void SpawnStunParticles(int milliseconds)
    {
        if (stunParticle == null) return;
        GameObject go = Instantiate(stunParticle, gameObject.transform, false);
        go.transform.localPosition = Vector3.zero;
        SafeDispose(go, milliseconds).Forget();
    }


    public void SpawnBatConfusionParticles()
    {
        GameObject go = Instantiate(confusionParticle, gameObject.transform, false);
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

