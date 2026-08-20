using Cysharp.Threading.Tasks;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class BatMaskStatusEffect : StatusEffect
{
    PlayerInputManager playerInput;
    public override StatsModifier GetStatsModifier() => null;

    public override void OnInitialize()
    {
        var player = StatusEffectManager?.gameObject;
        if (player == null) return;

        playerInput = player.GetComponent<PlayerInputManager>();
        playerInput?.ReverseControls();

        SpawnBatMaskVFX().Forget();
    }

    public async UniTask SpawnBatMaskVFX()
    {
        await UniTask.WaitUntil(() =>
            NetworkManager.Singleton.ConnectedClientsList.All(c => c.PlayerObject != null)
        );

        GameLogger.Log(LogSeverity.Debug, "Calling SpawnBatConfusion event...");
        NetworkVisualEffectManager.SpawnBatConfusionEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId);
    }  

    public override void OnEnd()
    {
        playerInput?.ResetControls();

        NetworkVisualEffectManager.RemoveBatConfusionEffectsOnPlayer?.Invoke(NetworkManager.Singleton.LocalClientId);
    }
}
