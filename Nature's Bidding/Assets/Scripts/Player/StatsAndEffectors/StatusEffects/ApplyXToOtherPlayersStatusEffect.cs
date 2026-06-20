using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public enum TargetPlayerType { Everyone, Self, NotSelf, Random }

public class ApplyXToOtherPlayersStatusEffect : StatusEffect
{
    protected TargetPlayerType Target;
    public override StatsModifier GetStatsModifier() => null;

    public ApplyXToOtherPlayersStatusEffect(TargetPlayerType targetType, List<StatusEffect> effectsToApply)
    {
        Target = targetType;
    }

    public override async void OnInitialize()
    {
        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton.ConnectedClientsList.All(x => x.PlayerObject != null)
        );

        ApplyToTargets();
    }

    private void ApplyToTargets()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        ulong selfId = Stats.playerData.clientId;

        switch (Target)
        {
            case TargetPlayerType.Everyone:
                foreach (var client in clients)
                    ApplyToPlayer(client.PlayerObject.gameObject);
                break;

            case TargetPlayerType.Self:
                var self = clients.FirstOrDefault(c => c.ClientId == selfId);
                if (self != null) ApplyToPlayer(self.PlayerObject.gameObject);
                break;

            case TargetPlayerType.NotSelf:
                foreach (var client in clients.Where(c => c.ClientId != selfId))
                    ApplyToPlayer(client.PlayerObject.gameObject);
                break;

            case TargetPlayerType.Random:
                var others = clients.Where(c => c.ClientId != selfId).ToList();
                if (others.Count > 0)
                {
                    var randomClient = others[Random.Range(0, others.Count)];
                    ApplyToPlayer(randomClient.PlayerObject.gameObject);
                }
                break;
        }
    }

    protected virtual void ApplyToPlayer(GameObject player) {

    }
}
