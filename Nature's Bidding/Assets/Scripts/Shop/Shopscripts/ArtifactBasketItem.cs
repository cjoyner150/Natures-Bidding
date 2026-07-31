using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ArtifactBasketItem : MonoBehaviour
{
    [Header("Animated container (child — not the root)")]
    [SerializeField] private RectTransform visual;

    [Header("Random Basket Rotation")]
    [SerializeField] private float minRandomRotation = -15f;
    [SerializeField] private float maxRandomRotation = 15f;

    [Header("Hover Pop-Out")]
    [SerializeField] private Vector2 hoverOffset = new Vector2(0f, 40f);
    [SerializeField] private float hoverScale = 1.2f;
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float unhoverDuration = 0.15f;

    [Header("Sorting While Popped Out")]
    [SerializeField] private int hoverSortingOrder = 500;

    private Vector2 _basketPosition;
    private float _basketRotation;
    private Vector2 _hoverPosition;
    private Vector3 _basketScale;
    private Vector3 _hoverScaleVector;

    private Canvas _visualCanvas;
    private CancellationTokenSource _animCts;

    private void Awake()
    {
        if (visual == null)
        {
            Debug.LogError($"[ArtifactBasketItem] 'visual' not assigned on {gameObject.name}.");
            return;
        }

        _basketPosition = visual.anchoredPosition;
        _basketRotation = UnityEngine.Random.Range(minRandomRotation, maxRandomRotation);
        visual.localRotation = Quaternion.Euler(0f, 0f, _basketRotation);

        _hoverPosition = _basketPosition + hoverOffset;

        _basketScale = visual.localScale;
        _hoverScaleVector = _basketScale * hoverScale;

        _visualCanvas = visual.GetComponent<Canvas>();
        if (_visualCanvas == null) _visualCanvas = visual.gameObject.AddComponent<Canvas>();
        _visualCanvas.overrideSorting = true;
        _visualCanvas.sortingOrder = 10;

        if (visual.GetComponent<GraphicRaycaster>() == null)
            visual.gameObject.AddComponent<GraphicRaycaster>();
    }

    public void PlayHoverOut()
    {
        if (_visualCanvas != null) _visualCanvas.sortingOrder = hoverSortingOrder;
        AnimateTo(_hoverPosition, 0f, _hoverScaleVector, hoverDuration).Forget();
    }

    public void PlayReturnToBasket()
    {
        if (this != null && _visualCanvas != null)
            _visualCanvas.sortingOrder = 10;

        AnimateReturnThenReset().Forget();
    }

    private async UniTaskVoid AnimateReturnThenReset()
    {
        await AnimateTo(_basketPosition, _basketRotation, _basketScale, unhoverDuration);

        if (this != null && _visualCanvas != null)
            _visualCanvas.sortingOrder = 10;
    }

    private async UniTask AnimateTo(Vector2 targetPos, float targetRotZ, Vector3 targetScale, float duration)
    {
        _animCts?.Cancel();
        _animCts?.Dispose();
        _animCts = new CancellationTokenSource();
        var token = _animCts.Token;

        if (visual == null) return;

        Vector2 startPos = visual.anchoredPosition;
        float startRot = NormalizeAngle(visual.localEulerAngles.z);
        Vector3 startScale = visual.localScale;

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        try
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                visual.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, easedT);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(startRot, targetRotZ, easedT));
                visual.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedT);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (this == null || visual == null) return;

        visual.anchoredPosition = targetPos;
        visual.localRotation = Quaternion.Euler(0f, 0f, targetRotZ);
        visual.localScale = targetScale;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    private void OnDestroy()
    {
        _animCts?.Cancel();
        _animCts?.Dispose();
    }
}