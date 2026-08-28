using UnityEngine;

[DisallowMultipleComponent]
public sealed class WwiseOneShotOnStart : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event eventToPost;

    [Tooltip("Post on the top-level owner instead of this short-lived effect. Use this for VFX that are destroyed before their sound finishes.")]
    [SerializeField] private bool postOnRoot;

    [Header("Optional Screen-Space Panning")]
    [SerializeField] private bool applyScreenSpacePan;
    [SerializeField] private AK.Wwise.RTPC combatPan;
    [SerializeField, Range(0f, 50f)] private float edgePanValue = 50f;
    [SerializeField] private Camera panCameraOverride;

    private void Start()
    {
        Post();
    }

    public void Post()
    {
        if (eventToPost == null || !eventToPost.IsValid())
        {
            Debug.LogWarning("[WwiseOneShotOnStart] No valid Wwise Event is assigned.", this);
            return;
        }

        GameObject emitter = postOnRoot ? transform.root.gameObject : gameObject;

        if (applyScreenSpacePan)
        {
            ApplyScreenSpacePan(emitter);
        }

        eventToPost.Post(emitter);
    }

    private void ApplyScreenSpacePan(GameObject emitter)
    {
        if (combatPan == null || !combatPan.IsValid())
        {
            Debug.LogWarning("[WwiseOneShotOnStart] Screen-space panning is enabled, but no valid Combat_Pan RTPC is assigned.", this);
            return;
        }

        if (!WwiseAudioUtility.TryApplyScreenSpacePan(
                combatPan,
                emitter,
                transform.position,
                edgePanValue,
                panCameraOverride))
        {
            Debug.LogWarning("[WwiseOneShotOnStart] No MainCamera was found. Combat_Pan was centered.", this);
        }
    }
}
