using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using OneLine;
using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyDummy : MonoBehaviour, IDamageable
{
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

    public void Hit(float damage, ulong fromPlayerId, out IDamageable.HitCallbackContext context, bool critical = false)
    {
        SpawnDamageNumbers(damage, critical);
        anim.SetTrigger("Hit");
        outlineBlink.StartBlinking();

        context = IDamageable.HitCallbackContext.success;
    }

    private void SpawnDamageNumbers(float damage, bool critical)
    {
        GameObject damageNumbersObj = Instantiate(damageNumbersPrefab, damageNumbersSpawnTransform.position, Quaternion.identity);
        RectTransform rectTransform = damageNumbersObj.GetComponent<RectTransform>();
        TextMeshProUGUI damageText = damageNumbersObj.GetComponentInChildren<TextMeshProUGUI>();

        damageText.text = damage.ToString("F0");
        damageText.color = critical ? Color.gold : Color.white;

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
}
