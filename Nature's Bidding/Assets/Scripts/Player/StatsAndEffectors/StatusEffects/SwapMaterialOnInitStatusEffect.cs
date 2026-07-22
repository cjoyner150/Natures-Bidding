using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class SwapMaterialOnInitStatusEffect : StatusEffect
{
    public override StatsModifier GetStatsModifier() => null;

    public override async void OnInitialize()
    {
        await UniTask.WaitUntil(() => NetworkManager.Singleton.ConnectedClientsList.All(c => c.PlayerObject != null));

        var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        playerObj.GetComponent<PlayerNetworkBehavior>().SwapMaterialOnPlayer();
    }
}