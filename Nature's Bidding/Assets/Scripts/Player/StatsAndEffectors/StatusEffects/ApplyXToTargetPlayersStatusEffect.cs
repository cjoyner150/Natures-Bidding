using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class ApplyXToTargetPlayersStatusEffect : StatusEffect
{
    protected TargetPlayerType target;
    protected List<StatusEffectorSO> effects;
    public override StatsModifier GetStatsModifier() => null;

    public ApplyXToTargetPlayersStatusEffect(TargetPlayerType targetType, List<StatusEffectorSO> effectsToApply)
    {
        target = targetType;
        effects = effectsToApply;
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
        var clients = NetworkManager.Singleton.ConnectedClientsList.ToList();
        ulong selfId = Stats.playerData.clientId;
        NetworkClient randomClient;

        switch (target)
        {
            case TargetPlayerType.Everyone:
                foreach (var client in clients)
                    ApplyToPlayer(client.ClientId);
                break;

            case TargetPlayerType.Self:
                var self = clients.FirstOrDefault(c => c.ClientId == selfId);
                if (self != null) ApplyToPlayer(self.ClientId);
                break;

            case TargetPlayerType.NotSelf:
                foreach (var client in clients.Where(c => c.ClientId != selfId))
                    ApplyToPlayer(client.ClientId);
                break;

            case TargetPlayerType.RandomExclusive:
                var others = clients.Where(c => c.ClientId != selfId).ToList();
                if (others.Count > 0)
                {
                    randomClient = others[Random.Range(0, others.Count)];
                    ApplyToPlayer(randomClient.ClientId);
                }
                break;

            case TargetPlayerType.Random:
                
                randomClient = clients[Random.Range(0, clients.Count)];
                ApplyToPlayer(randomClient.ClientId);
                
                break;
        }
    }

    protected void ApplyToPlayer(ulong targetClientId)
    {
        string[] effectIds = effects.Select(x => x.Id).ToArray();
        Debug.Log($"Sending effects ({string.Join(", ", effectIds)}) to {targetClientId}");
        StatusEffectNetworkManager.Instance.ApplyToPlayerServerRpc(targetClientId, string.Join(",", effectIds));
    }
}
