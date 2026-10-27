using UnityEngine.Serialization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AkGameObj))]
public sealed class PlayerAudioFeedback : MonoBehaviour
{
    private const float DeathEmitterLifetimeSeconds = 2f;

    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event doubleJumpEvent;
    [SerializeField] private AK.Wwise.Event deathEvent;
    [SerializeField] private AK.Wwise.Event warpEvent;
    [SerializeField] private AK.Wwise.Event fallEvent;
    [SerializeField] private AK.Wwise.Event firstJumpGruntEvent;
    [SerializeField] private AK.Wwise.Switch[] playerVoiceSwitches;
    [SerializeField] private AK.Wwise.Event shieldUpEvent;
    [SerializeField] private AK.Wwise.Event shieldFailEvent;

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
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_GP_Player_DoubleJump Event is assigned.", this);
            return;
        }

        ApplyScreenSpacePan(gameObject, transform.position);

        doubleJumpEvent.Post(gameObject);
    }

    public void PlayWarp()
    {
        if (warpEvent == null || !warpEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_GP_Player_Warp Event is assigned.", this);
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
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_GP_Player_Death Event is assigned.", this);
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

    public void PlayShieldFail()
    {
        if (shieldFailEvent == null || !shieldFailEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_GP_Player_Shield_Fail Event is assigned.", this);
            return;
        }
        ApplyScreenSpacePan(gameObject, transform.position);
        shieldFailEvent.Post(gameObject);
    }

    public void PlayShieldUp()
    {
        if (shieldUpEvent == null || !shieldUpEvent.IsValid())
        {
            Debug.LogWarning("[PlayerAudioFeedback] No valid Play_GP_Player_Shield Event is assigned.", this);
            return;
        }
        ApplyScreenSpacePan(gameObject, transform.position);
        shieldUpEvent.Post(gameObject);
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
