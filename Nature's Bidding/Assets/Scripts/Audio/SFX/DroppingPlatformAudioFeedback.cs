using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DroppingPlatformAudioFeedback : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event rockSlideEvent;

    [Header("Screen-Space Panning")]
    [SerializeField] private bool applyScreenSpacePan = true;
    [SerializeField] private AK.Wwise.RTPC combatPan;
    [SerializeField, Range(0f, 50f)] private float edgePanValue = 50f;
    [SerializeField] private Camera panCameraOverride;

    [Header("Movement Detection")]
    [SerializeField, Min(0f)] private float minimumDownwardMovement = 0.02f;
    [SerializeField, Min(0f)] private float startupGraceSeconds = 1f;

    private readonly List<Transform> platforms = new List<Transform>();
    private readonly Dictionary<Transform, float> baselineHeights = new Dictionary<Transform, float>();
    private readonly HashSet<Transform> triggeredPlatforms = new HashSet<Transform>();
    private float enabledAt;

    private void OnEnable()
    {
        enabledAt = Time.time;
        platforms.Clear();
        baselineHeights.Clear();
        triggeredPlatforms.Clear();

        NetworkTransform[] replicatedPlatforms = GetComponentsInChildren<NetworkTransform>(true);
        foreach (NetworkTransform replicatedPlatform in replicatedPlatforms)
        {
            if (replicatedPlatform == null)
                continue;

            Transform platform = replicatedPlatform.transform;
            platforms.Add(platform);
            baselineHeights[platform] = platform.position.y;
        }

        if (platforms.Count == 0)
            Debug.LogWarning("[DroppingPlatformAudioFeedback] No replicated platform transforms were found.", this);
    }

    private void LateUpdate()
    {
        bool canTrigger = Time.time - enabledAt >= startupGraceSeconds;

        foreach (Transform platform in platforms)
        {
            if (platform == null || triggeredPlatforms.Contains(platform))
            {
                continue;
            }

            float currentHeight = platform.position.y;
            if (!baselineHeights.TryGetValue(platform, out float baselineHeight))
            {
                baselineHeights[platform] = currentHeight;
                continue;
            }

            // Consume replication corrections during startup. Once the grace period
            // ends, keep a fixed baseline so slow movement accumulates across frames.
            if (!canTrigger)
            {
                baselineHeights[platform] = currentHeight;
                continue;
            }

            if (baselineHeight - currentHeight < minimumDownwardMovement)
            {
                continue;
            }

            triggeredPlatforms.Add(platform);
            PostRockSlide(platform);
        }
    }

    private void PostRockSlide(Transform platform)
    {
        if (rockSlideEvent == null || !rockSlideEvent.IsValid())
        {
            Debug.LogWarning("[DroppingPlatformAudioFeedback] No valid Play_SFX_RockSlide Event is assigned.", this);
            return;
        }

        GameObject emitter = platform.gameObject;

        if (applyScreenSpacePan && combatPan != null && combatPan.IsValid())
        {
            WwiseAudioUtility.TryApplyScreenSpacePan(
                combatPan,
                emitter,
                platform.position,
                edgePanValue,
                panCameraOverride);
        }

        rockSlideEvent.Post(emitter);
    }
}
