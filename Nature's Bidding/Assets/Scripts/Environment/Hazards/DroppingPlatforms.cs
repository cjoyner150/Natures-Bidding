using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class DroppingPlatforms : NetworkBehaviour, IHazardSystem
{
    [SerializeField] AnimationCurve fallAnimCurve;
    [SerializeField] List<Transform> platforms;
    [SerializeField] float dropDistance;
    [SerializeField] float startDelay;
    [SerializeField] float dropWaitTime;
    [SerializeField] float fallTime;

    float _dropWaitTimer;

    bool tickingAllowed = false;

    public void StartHazard()
    {
        if (!IsServer) return;

        BeginDroppingPlatforms();
    }


    public void StopHazard()
    {
        if (!IsServer) return;

        tickingAllowed = false;
    }

    public void TickHazard(float delta)
    {
        if (!IsServer || !tickingAllowed) return;

        if (_dropWaitTimer > 0)
        {
            _dropWaitTimer -= delta;
            
            if (_dropWaitTimer <= 0)
            {
                TriggerHazard();
                _dropWaitTimer = dropWaitTime;
            }

            return;
        }
    }

    public void TriggerHazard()
    {
        if (!IsServer) return;

        DropRandomPlatform();
    }

    private async void DropRandomPlatform()
    {
        if (platforms.Count == 0) return;

        int rand = Random.Range(0, platforms.Count);
        Transform plat = platforms[rand];
        Vector3 startPos = plat.position;
        float targetY = startPos.y - dropDistance;

        platforms.RemoveAt(rand); // prevent re-selecting a platform that's already dropping

        await AnimateDrop(plat, startPos, targetY);
    }

    private async UniTask AnimateDrop(Transform plat, Vector3 startPos, float targetY)
    {
        float elapsed = 0f;

        while (elapsed < fallTime)
        {
            if (plat == null) return; // guard against despawn mid-animation

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallTime);
            float curveValue = fallAnimCurve.Evaluate(t);

            Vector3 newPos = startPos;
            newPos.y = Mathf.Lerp(startPos.y, targetY, curveValue);
            plat.position = newPos;

            await UniTask.Yield();
        }

        if (plat != null)
        {
            Vector3 finalPos = startPos;
            finalPos.y = targetY;
            plat.position = finalPos;
        }
    }

    private async void BeginDroppingPlatforms()
    {
        await UniTask.WaitForSeconds(startDelay);

        _dropWaitTimer = dropWaitTime;
        tickingAllowed = true;
    }


}
