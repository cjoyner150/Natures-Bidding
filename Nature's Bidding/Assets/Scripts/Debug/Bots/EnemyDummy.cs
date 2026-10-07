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

    public override void OnNetworkSpawn()
    {
        dummyCtx.playerEffectable = this;
        dummyCtx.playerDamageable = this;

        var statsMediator = new StatsMediator();
        dummyCtx.playerStats = new Stats(statsMediator, dummyCtx.BaseStats, PersistentPlayerRegistry.Instance.GetByClientId(0));
        dummyCtx.maxJumps = dummyCtx.playerStats.Jumps;

        PlayerAttackManager attackManager = GetComponent<PlayerAttackManager>();
        dummyCtx.playerAttackManager = attackManager;
        attackManager.Initialize(dummyCtx);

        var inputManager = GetComponent<DummyInputManager>();
        inputManager.InitializePlayer(dummyCtx);
    }

    public void Hit(float damage, PlayerContext fromPlayerCtx, out IDamageable.HitCallbackContext context, bool critical = false)
    {
        SpawnDamageNumbers(damage, critical ? Color.gold : Color.white);
        //anim.SetTrigger("Hit");
        outlineBlink.StartBlinking();

        NetworkVisualEffectManager.SpawnHitReactionEffectsOnPlayer?.Invoke(dummyCtx, critical, fromPlayerCtx.rb.position, damage);
        dummyCtx.lastHitFromPosition = fromPlayerCtx.rb.position;
        dummyCtx.shouldTakeKnockback = true;

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

    public void TickHealth(float damage, PlayerContext fromContext)
    {
        Hit(damage, fromContext, out var _ctx);
    }

    public void StealFrom(long thiefId, long targetId, int amount)
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy lost {amount} gold");

        if (thiefId >= 0)
            PersistentPlayerRegistry.Instance.AddGold((ulong)thiefId, amount);
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

    public void Recover()
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy is no longer stunned");
    }

    public void BeginParry()
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy is parrying");
    }

    public void EndParry()
    {
        GameLogger.Log(LogSeverity.Debug, $"EnemyDummy is no longer parrying");
    }
}

public enum DummyState
{
    idle,
    attacking,
    fallAttacking,
    jumpAttacking,
    walking,
    dashing,
    jumping
}
