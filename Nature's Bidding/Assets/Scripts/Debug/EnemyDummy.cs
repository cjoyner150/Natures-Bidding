using Cysharp.Threading.Tasks;
using HSM;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyDummy : NetworkBehaviour, IDamageable, IEffectable
{
    [SerializeField] PlayerContext dummyCtx;

    [Header("References")]
    [SerializeField] Animator anim;
    [SerializeField] SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] MMBlink outlineBlink;

    [Header("Damage Numbers")]
    [SerializeField] Transform damageNumbersSpawnTransform;
    [SerializeField] GameObject damageNumbersPrefab;

    [Tooltip("Base direction damage numbers travel, in local/screen space. (0,1) is straight up.")]
    [SerializeField] private Vector2 baseDirection = Vector2.up;

    [Tooltip("Max angle (degrees) of random deviation from baseDirection, applied both left and right.")]
    [SerializeField] private float maxAngleSpread = 35f;

    [Header("Distance")]
    [SerializeField] private float minDistance = 40f;
    [SerializeField] private float maxDistance = 80f;

    private State root;
    private StateMachine sm;
    private bool initialized = false;

    public override void OnNetworkSpawn()
    {
        var statsMediator = new StatsMediator();
        dummyCtx.playerStats = new Stats(statsMediator, dummyCtx.BaseStats, PersistentPlayerRegistry.Instance.GetByClientId(0));
        dummyCtx.maxJumps = dummyCtx.playerStats.Jumps;

        PlayerAttackManager attackManager = GetComponent<PlayerAttackManager>();
        dummyCtx.playerAttackManager = attackManager;
        attackManager.Initialize(dummyCtx);

        root = new PlayerRoot(null, dummyCtx);
        var builder = new StateMachineBuilder(root);

        sm = builder.Build();

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        HandleOrientation();
        dummyCtx.isGrounded = CheckGrounded();

        sm.Tick(Time.deltaTime);
    }

    private void FixedUpdate() => HandlePhysicsMove();
    
    
    private void HandlePhysicsMove()
    {
        if (!initialized) return;
        dummyCtx.rb.AddForce(dummyCtx.forceToAdd * Time.fixedDeltaTime, dummyCtx.forceMode);
    }

    private void HandleOrientation()
    {
        Vector3 cameraRelativeOrientation = dummyCtx.cam.transform.forward;
        cameraRelativeOrientation.y = 0;
        cameraRelativeOrientation = cameraRelativeOrientation.normalized;

        dummyCtx.orientation.forward = cameraRelativeOrientation;
    }

    private bool CheckGrounded()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position + (transform.up * .125f), .25f, dummyCtx.isGroundLayers);
        return colliders.Length > 0;
    }

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context, bool critical = false)
    {
        SpawnDamageNumbers(damage, critical ? Color.gold : Color.white);
        //anim.SetTrigger("Hit");
        outlineBlink.StartBlinking();

        context = IDamageable.HitCallbackContext.success;
    }

    private void SpawnDamageNumbers(float damage, Color color)
    {
        GameObject damageNumbersObj = Instantiate(damageNumbersPrefab, damageNumbersSpawnTransform.position, Quaternion.identity);
        RectTransform rectTransform = damageNumbersObj.GetComponent<RectTransform>();
        TextMeshProUGUI damageText = damageNumbersObj.GetComponentInChildren<TextMeshProUGUI>();

        damageText.text = damage.ToString("F0");
        damageText.color = color;

        Transform cam = Camera.main.transform;
        MMFaceDirection faceDirection = damageNumbersObj.GetComponent<MMFaceDirection>();
        faceDirection.FacingTarget = cam;

        float randomAngle = Random.Range(-maxAngleSpread, maxAngleSpread);
        Vector3 worldDirection = Quaternion.AngleAxis(randomAngle, cam.forward) * cam.up;

        float distance = Random.Range(minDistance, maxDistance);
        Vector3 worldOffset = worldDirection * distance;

        MMSpringRectTransformPosition spring = damageNumbersObj.GetComponent<MMSpringRectTransformPosition>();
        if (spring != null)
        {
            Vector3 localOffset = damageNumbersObj.transform.InverseTransformDirection(worldOffset);
            Vector3 targetLocalPos = rectTransform.localPosition + localOffset;
            spring.MoveTo(targetLocalPos);
        }

        _ = SafeDispose(damageNumbersObj, 4000);
    }

    private UniTask SafeDispose(GameObject obj, int msDelay)
    {
        return UniTask.Delay(msDelay).ContinueWith(() => { if (obj != null) Destroy(obj); });
    }

    public void TickHealth(float damage, ulong fromPlayerId)
    {
        Hit(damage, fromPlayerId, out var _ctx);
    }

    public void StealFrom(ulong thiefId, ulong targetId, int amount)
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy lost {amount} gold");
        PersistentPlayerRegistry.Instance.AddGold(thiefId, amount);
    }

    public void Heal(float amount)
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy healed {amount}");
        SpawnDamageNumbers(amount, Color.green);
    }

    public void Stun(float additionalStunTime)
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy is stunned");
        dummyCtx.shouldStunSelf = true;
    }
}
