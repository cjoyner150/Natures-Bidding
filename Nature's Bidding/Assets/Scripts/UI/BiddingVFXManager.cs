using Cysharp.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.VisualScripting;
using MoreMountains.Feedbacks;
using UnityEngine;

public class BiddingVFXManager : MonoBehaviour
{
    [Header("Particle Prefabs")]
    [SerializeField] GameObject coinTransfer;

    public void SpawnCoinTransfer(int coinCount, GameObject target, GameObject source) //pass number of coins, and gameobject to target.
    {
        GameObject go = Instantiate(coinTransfer, source.Transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        SafeDispose(go, coinCoint/30).Forget();
    }



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
