using UnityEngine.Serialization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AkGameObj))]
public sealed class PlayerAudioFeedback : MonoBehaviour
{
    private const float DeathEmitterLifetimeSeconds = 2f;

    [Header("Wwise")]
    [FormerlySerializedAs("jumpEvent")]
    [SerializeField] private AK.Wwise.Event doubleJumpEvent;
    [SerializeField] private AK.Wwise.Event deathEvent;
    [SerializeField] private AK.Wwise.Event warpEvent;
    [SerializeField] private AK.Wwise.Event fallEvent;
    [FormerlySerializedAs("jumpGruntEvent")]
    [SerializeField] private AK.Wwise.Event firstJumpGruntEvent;
    [SerializeField] private AK.Wwise.Switch[] playerVoiceSwitches;

    [Header("Screen-Space Panning")]
    [SerializeField] private AK.Wwise.RTPC combatPan;
    [SerializeField, Range(0f, 50f)] private float edgePanValue = 50f;
    [SerializeField] private Camera panCameraOverride;

    private bool deathSoundPlayed;

    public void PlayJump(bool isDoubleJump)
    {
        if (isDoubleJump)
            PlayDoubleJump();
        else
            PlayFirstJumpGrunt();
    }

    private void PlayDoubleJump()
    {
        if (doubleJumpEvent == null || !doubleJumpEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_SFX_DoubleJump Event is assigned.", this);
            return;
        }

        ApplyScreenSpacePan(gameObject, transform.position);

        doubleJumpEvent.Post(gameObject);
    }

    public void PlayWarp()
    {
        if (warpEvent == null || !warpEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_SFX_Warp Event is assigned.", this);
            return;
        }

        ApplyScreenSpacePan(gameObject, transform.position);
        warpEvent.Post(gameObject);
    }

    public void PlayDeath()
    {
        if (deathSoundPlayed)
            return;

        if (deathEvent == null || !deathEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_SFX_Death Event is assigned.", this);
            return;
        }

        deathSoundPlayed = true;

        var emitter = new GameObject($"{gameObject.name} Death Audio");
        emitter.transform.position = transform.position;
        emitter.AddComponent<AkGameObj>();

        ApplyScreenSpacePan(emitter, emitter.transform.position);
        deathEvent.Post(emitter);
        Destroy(emitter, DeathEmitterLifetimeSeconds);
    }

    public void PlayFall()
    {
        if (fallEvent == null || !fallEvent.IsValid())
            return;
        
        var emitter = new GameObject("Fall Sound");
        emitter.transform.position = transform.position;
        emitter.AddComponent<AkGameObj>();

        fallEvent.Post(emitter);
        Destroy(emitter, 5f);
    }

    private void PlayFirstJumpGrunt()
    {
        if (firstJumpGruntEvent == null || !firstJumpGruntEvent.IsValid())
            return;

        var player = GetComponent<PlayerNetworkBehavior>();
        var registry = PersistentPlayerRegistry.Instance;

        if (player == null || registry == null)
            return;

        var data = registry.GetByClientId(player.OwnerClientId);
        if (data == null)
            return;

        int index = data.playerIndex;
        if (playerVoiceSwitches == null || index < 0 || index >= playerVoiceSwitches.Length)
            return;

        var voice = playerVoiceSwitches[index];
        if (voice == null || !voice.IsValid())
            return;

        voice.SetValue(gameObject);
        ApplyScreenSpacePan(gameObject, transform.position);
        firstJumpGruntEvent.Post(gameObject);
    }

    private void ApplyScreenSpacePan(GameObject emitter, Vector3 worldPosition)
    {
        if (combatPan == null || !combatPan.IsValid())
            return;

        WwiseAudioUtility.TryApplyScreenSpacePan(
            combatPan,
            emitter,
            worldPosition,
            edgePanValue,
            panCameraOverride);
    }
}
